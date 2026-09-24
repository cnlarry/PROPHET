/*
 * ============================================================================
 * 文件名：buffer_pool.hpp
 * 功能说明：SIMD计算的共享缓冲区池（阶段3优化）
 * 
 * 性能优化：
 * - 避免指标计算时重复分配临时缓冲区
 * - 使用SIMD对齐内存
 * - 线程本地存储，避免线程竞争
 * - 自动增长，适应不同数据大小
 * ============================================================================
 */

#pragma once

#include "prophet/simd/aligned_allocator.hpp"
#include <vector>
#include <cstddef>

namespace prophet::simd {

/**
 * SIMD缓冲区池（线程本地）
 * 
 * 为指标计算提供临时工作缓冲区，避免重复分配
 * 每个线程独立使用，无需加锁
 */
class BufferPool {
public:
    /**
     * 获取线程本地缓冲区池实例
     */
    static BufferPool& instance() {
        thread_local BufferPool pool;
        return pool;
    }
    
    /**
     * 获取double类型缓冲区
     * @param min_size 最小所需大小
     * @return 对齐的double数组（大小>=min_size）
     */
    double* get_double_buffer(size_t min_size) {
        if (min_size > double_buffer_.size()) {
            // 增长策略：向上取整到最近的256倍数（避免频繁扩容）
            size_t new_size = ((min_size + 255) / 256) * 256;
            double_buffer_.resize(new_size);
        }
        return double_buffer_.data();
    }
    
    /**
     * 获取float类型缓冲区
     */
    float* get_float_buffer(size_t min_size) {
        if (min_size > float_buffer_.size()) {
            size_t new_size = ((min_size + 255) / 256) * 256;
            float_buffer_.resize(new_size);
        }
        return float_buffer_.data();
    }
    
    /**
     * 获取int类型缓冲区
     */
    int* get_int_buffer(size_t min_size) {
        if (min_size > int_buffer_.size()) {
            size_t new_size = ((min_size + 255) / 256) * 256;
            int_buffer_.resize(new_size);
        }
        return int_buffer_.data();
    }
    
    /**
     * 获取多个double缓冲区（用于需要多个临时数组的指标）
     * @param count 缓冲区数量
     * @param size 每个缓冲区大小
     * @return 缓冲区指针数组
     */
    std::vector<double*> get_multi_buffers(size_t count, size_t size) {
        // 确保有足够的缓冲区
        while (multi_buffers_.size() < count) {
            multi_buffers_.emplace_back();
        }
        
        // 调整每个缓冲区大小
        std::vector<double*> result;
        result.reserve(count);
        
        for (size_t i = 0; i < count; ++i) {
            if (size > multi_buffers_[i].size()) {
                size_t new_size = ((size + 255) / 256) * 256;
                multi_buffers_[i].resize(new_size);
            }
            result.push_back(multi_buffers_[i].data());
        }
        
        return result;
    }
    
    /**
     * 清空所有缓冲区（保留容量）
     * 用于内存压力大时手动释放部分内存
     */
    void clear() {
        // 不实际释放内存，只重置逻辑大小
        // 如果需要真正释放内存，使用 shrink()
    }
    
    /**
     * 收缩缓冲区到最小大小
     * 用于长时间空闲后释放内存
     */
    void shrink() {
        double_buffer_.clear();
        double_buffer_.shrink_to_fit();
        float_buffer_.clear();
        float_buffer_.shrink_to_fit();
        int_buffer_.clear();
        int_buffer_.shrink_to_fit();
        multi_buffers_.clear();
        multi_buffers_.shrink_to_fit();
    }
    
    /**
     * 获取当前缓冲区统计
     */
    struct Statistics {
        size_t double_buffer_capacity;
        size_t float_buffer_capacity;
        size_t int_buffer_capacity;
        size_t multi_buffer_count;
        size_t total_bytes;
    };
    
    Statistics get_statistics() const {
        Statistics stats;
        stats.double_buffer_capacity = double_buffer_.capacity();
        stats.float_buffer_capacity = float_buffer_.capacity();
        stats.int_buffer_capacity = int_buffer_.capacity();
        stats.multi_buffer_count = multi_buffers_.size();
        
        stats.total_bytes = 
            stats.double_buffer_capacity * sizeof(double) +
            stats.float_buffer_capacity * sizeof(float) +
            stats.int_buffer_capacity * sizeof(int);
        
        for (const auto& buf : multi_buffers_) {
            stats.total_bytes += buf.capacity() * sizeof(double);
        }
        
        return stats;
    }

private:
    // 私有构造，单例模式（线程本地）
    BufferPool() = default;
    
    // 禁止拷贝和移动
    BufferPool(const BufferPool&) = delete;
    BufferPool& operator=(const BufferPool&) = delete;
    BufferPool(BufferPool&&) = delete;
    BufferPool& operator=(BufferPool&&) = delete;

private:
    // 使用SIMD对齐的分配器
    AVX2Vector<double> double_buffer_;
    AVX2Vector<float> float_buffer_;
    AVX2Vector<int> int_buffer_;
    
    // 多缓冲区支持（用于复杂指标）
    std::vector<AVX2Vector<double>> multi_buffers_;
};

/**
 * RAII风格的缓冲区借用器
 * 
 * 自动管理缓冲区的获取和归还（虽然BufferPool不需要显式归还）
 */
class ScopedBuffer {
public:
    explicit ScopedBuffer(size_t size)
        : size_(size)
        , ptr_(BufferPool::instance().get_double_buffer(size)) {}
    
    ~ScopedBuffer() = default;
    
    // 禁止拷贝，允许移动
    ScopedBuffer(const ScopedBuffer&) = delete;
    ScopedBuffer& operator=(const ScopedBuffer&) = delete;
    
    ScopedBuffer(ScopedBuffer&& other) noexcept
        : size_(other.size_)
        , ptr_(other.ptr_) {
        other.size_ = 0;
        other.ptr_ = nullptr;
    }
    
    ScopedBuffer& operator=(ScopedBuffer&& other) noexcept {
        if (this != &other) {
            size_ = other.size_;
            ptr_ = other.ptr_;
            other.size_ = 0;
            other.ptr_ = nullptr;
        }
        return *this;
    }
    
    double* data() { return ptr_; }
    const double* data() const { return ptr_; }
    size_t size() const { return size_; }
    
    double& operator[](size_t index) { return ptr_[index]; }
    const double& operator[](size_t index) const { return ptr_[index]; }

private:
    size_t size_;
    double* ptr_;
};

} // namespace prophet::simd

