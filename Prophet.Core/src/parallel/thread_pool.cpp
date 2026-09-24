/*
 * ============================================================================
 * 文件名：thread_pool.cpp
 * 功能说明：统一线程池实现
 * ============================================================================
 */

#include "prophet/parallel/thread_pool.hpp"
#include <iostream>

namespace prophet::parallel {

// ============================================================================
// ThreadPool 实现
// ============================================================================

ThreadPool::ThreadPool(size_t num_threads)
    : stop_(false)
    , busy_threads_(0)
{
    // 如果未指定线程数，使用硬件并发数
    if (num_threads == 0) {
        num_threads = std::thread::hardware_concurrency();
        if (num_threads == 0) {
            num_threads = 4;  // 默认4个线程
        }
    }
    
    // 预创建工作线程
    workers_.reserve(num_threads);
    for (size_t i = 0; i < num_threads; ++i) {
        workers_.emplace_back([this] { worker_thread(); });
    }
}

ThreadPool::~ThreadPool() {
    {
        std::unique_lock<std::mutex> lock(queue_mutex_);
        stop_ = true;
    }
    
    // 通知所有线程
    condition_.notify_all();
    
    // 等待所有线程结束
    for (std::thread& worker : workers_) {
        if (worker.joinable()) {
            worker.join();
        }
    }
}

size_t ThreadPool::pending_tasks() const {
    std::unique_lock<std::mutex> lock(const_cast<std::mutex&>(queue_mutex_));
    return tasks_.size();
}

void ThreadPool::wait_all() {
    std::unique_lock<std::mutex> lock(queue_mutex_);
    wait_condition_.wait(lock, [this] {
        return tasks_.empty() && busy_threads_ == 0;
    });
}

void ThreadPool::worker_thread() {
    while (true) {
        std::function<void()> task;
        
        {
            std::unique_lock<std::mutex> lock(queue_mutex_);
            
            // 等待任务或停止信号
            condition_.wait(lock, [this] {
                return stop_ || !tasks_.empty();
            });
            
            // 如果停止且没有任务，退出
            if (stop_ && tasks_.empty()) {
                return;
            }
            
            // 取出任务
            if (!tasks_.empty()) {
                task = std::move(tasks_.front());
                tasks_.pop();
                ++busy_threads_;
            }
        }
        
        // 执行任务（不持有锁）
        if (task) {
            try {
                task();
            } catch (const std::exception& e) {
                std::cerr << "ThreadPool: Exception in task: " << e.what() << std::endl;
            } catch (...) {
                std::cerr << "ThreadPool: Unknown exception in task" << std::endl;
            }
            
            {
                std::unique_lock<std::mutex> lock(queue_mutex_);
                --busy_threads_;
                wait_condition_.notify_all();
            }
        }
    }
}

// ============================================================================
// GlobalThreadPool 实现
// ============================================================================

ThreadPool& GlobalThreadPool::instance(size_t num_threads) {
    static ThreadPool pool(num_threads);
    return pool;
}

} // namespace prophet::parallel

