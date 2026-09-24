/*
 * ============================================================================
 * 文件名：thread_pool.hpp
 * 功能说明：统一线程池 - Prophet Core天然并行架构基础设施
 * 
 * 设计理念：
 * - 预创建线程，避免重复创建/销毁开销
 * - 任务队列，支持异步提交
 * - 优雅关闭，确保所有任务完成
 * - 线程安全，无锁设计（条件变量）
 * 
 * 使用场景：
 * - K线转换并行化
 * - 指标计算并行化
 * - 表达式求值并行化
 * - 批量策略评估
 * ============================================================================
 */

#pragma once

#include <vector>
#include <queue>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <functional>
#include <future>
#include <memory>
#include <stdexcept>

namespace prophet::parallel {

/**
 * 统一线程池
 * 
 * 特性：
 * - 预创建固定数量的工作线程
 * - 支持任意可调用对象（lambda、函数指针、函数对象）
 * - 返回std::future以获取异步结果
 * - 自动管理任务队列
 * - RAII风格，析构时自动等待所有任务完成
 */
class ThreadPool {
public:
    /**
     * 构造线程池
     * @param num_threads 线程数（0表示自动检测硬件并发数）
     */
    explicit ThreadPool(size_t num_threads = 0);
    
    /**
     * 析构函数（等待所有任务完成并关闭线程池）
     */
    ~ThreadPool();
    
    // 禁止拷贝
    ThreadPool(const ThreadPool&) = delete;
    ThreadPool& operator=(const ThreadPool&) = delete;
    
    /**
     * 提交任务到线程池
     * 
     * @tparam F 可调用对象类型
     * @tparam Args 参数类型
     * @param f 可调用对象（函数、lambda、函数对象）
     * @param args 参数
     * @return std::future<返回类型> 用于获取异步结果
     * 
     * 示例：
     *   auto future = pool.submit([]() { return 42; });
     *   int result = future.get();  // 阻塞等待结果
     */
    template<typename F, typename... Args>
    auto submit(F&& f, Args&&... args) 
        -> std::future<typename std::invoke_result<F, Args...>::type>;
    
    /**
     * 获取线程数
     */
    size_t num_threads() const { return workers_.size(); }
    
    /**
     * 获取待处理任务数量
     */
    size_t pending_tasks() const;
    
    /**
     * 等待所有任务完成（不关闭线程池）
     */
    void wait_all();
    
private:
    // 工作线程列表
    std::vector<std::thread> workers_;
    
    // 任务队列
    std::queue<std::function<void()>> tasks_;
    
    // 同步原语
    std::mutex queue_mutex_;
    std::condition_variable condition_;
    std::condition_variable wait_condition_;
    
    // 状态标志
    bool stop_;
    size_t busy_threads_;
    
    /**
     * 工作线程函数
     */
    void worker_thread();
};

// ============================================================================
// 模板实现
// ============================================================================

template<typename F, typename... Args>
auto ThreadPool::submit(F&& f, Args&&... args) 
    -> std::future<typename std::invoke_result<F, Args...>::type>
{
    using return_type = typename std::invoke_result<F, Args...>::type;
    
    // 创建packaged_task（绑定参数）
    auto task = std::make_shared<std::packaged_task<return_type()>>(
        std::bind(std::forward<F>(f), std::forward<Args>(args)...)
    );
    
    std::future<return_type> result = task->get_future();
    
    {
        std::unique_lock<std::mutex> lock(queue_mutex_);
        
        // 检查线程池是否已停止
        if (stop_) {
            throw std::runtime_error("Cannot submit task to stopped ThreadPool");
        }
        
        // 将任务包装为void()并加入队列
        tasks_.emplace([task]() { (*task)(); });
    }
    
    // 通知一个工作线程
    condition_.notify_one();
    
    return result;
}

// ============================================================================
// 全局单例（可选）
// ============================================================================

/**
 * 全局线程池单例
 * 
 * 使用场景：
 * - 需要在整个应用中共享线程池
 * - 避免多个模块各自创建线程池
 * 
 * 注意：
 * - 线程数在首次访问时确定（默认为硬件并发数）
 * - 生命周期由C++运行时管理（程序结束时析构）
 */
class GlobalThreadPool {
public:
    /**
     * 获取全局线程池实例
     * @param num_threads 线程数（仅首次调用有效，后续调用忽略）
     */
    static ThreadPool& instance(size_t num_threads = 0);
    
private:
    GlobalThreadPool() = delete;
};

} // namespace prophet::parallel

