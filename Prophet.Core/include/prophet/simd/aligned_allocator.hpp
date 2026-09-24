/*
 * ============================================================================
 * 文件名：aligned_allocator.hpp
 * 功能说明：SIMD对齐内存分配器（阶段3优化）
 * 
 * 性能优化：
 * - 为AVX2/AVX-512指令提供对齐内存（32字节/64字节对齐）
 * - 避免非对齐访问的性能损失（30-50%的性能差异）
 * - 兼容STL容器（std::vector、std::array等）
 * ============================================================================
 */

#pragma once

#include <cstddef>
#include <cstdlib>
#include <memory>
#include <new>

#ifdef _WIN32
#include <malloc.h>
#else
#include <stdlib.h>
#endif

namespace prophet::simd {

/**
 * 对齐内存分配器
 * 
 * 符合C++17 Allocator要求，可用于STL容器
 * 
 * @tparam T 元素类型
 * @tparam Alignment 对齐字节数（必须是2的幂，默认32字节用于AVX2）
 */
template<typename T, size_t Alignment = 32>
class AlignedAllocator {
public:
    using value_type = T;
    using pointer = T*;
    using const_pointer = const T*;
    using reference = T&;
    using const_reference = const T&;
    using size_type = std::size_t;
    using difference_type = std::ptrdiff_t;
    
    // C++17 Allocator要求
    using propagate_on_container_move_assignment = std::true_type;
    using is_always_equal = std::true_type;
    
    template<typename U>
    struct rebind {
        using other = AlignedAllocator<U, Alignment>;
    };
    
    // 构造函数
    AlignedAllocator() noexcept = default;
    
    template<typename U>
    AlignedAllocator(const AlignedAllocator<U, Alignment>&) noexcept {}
    
    // 分配对齐内存
    [[nodiscard]] T* allocate(size_type n) {
        if (n == 0) {
            return nullptr;
        }
        
        // 检查对齐参数有效性
        static_assert((Alignment & (Alignment - 1)) == 0, 
                      "Alignment must be a power of 2");
        static_assert(Alignment >= alignof(T), 
                      "Alignment must be at least alignof(T)");
        
        // 计算所需字节数
        const size_type bytes = n * sizeof(T);
        
        // 对齐内存分配（跨平台）
        void* ptr = nullptr;
        
#ifdef _WIN32
        ptr = _aligned_malloc(bytes, Alignment);
#else
        if (posix_memalign(&ptr, Alignment, bytes) != 0) {
            ptr = nullptr;
        }
#endif
        
        if (!ptr) {
            throw std::bad_alloc();
        }
        
        return static_cast<T*>(ptr);
    }
    
    // 释放内存
    void deallocate(T* ptr, size_type) noexcept {
        if (!ptr) {
            return;
        }
        
#ifdef _WIN32
        _aligned_free(ptr);
#else
        free(ptr);
#endif
    }
    
    // 比较运算符
    template<typename U, size_t A>
    bool operator==(const AlignedAllocator<U, A>&) const noexcept {
        return Alignment == A;
    }
    
    template<typename U, size_t A>
    bool operator!=(const AlignedAllocator<U, A>&) const noexcept {
        return Alignment != A;
    }
};

/**
 * 对齐内存RAII包装
 * 
 * 用于手动管理单个对齐内存块
 */
template<typename T, size_t Alignment = 32>
class AlignedMemory {
public:
    AlignedMemory() = default;
    
    explicit AlignedMemory(size_t size)
        : size_(size)
        , ptr_(allocate(size)) {}
    
    ~AlignedMemory() {
        deallocate();
    }
    
    // 禁止拷贝，允许移动
    AlignedMemory(const AlignedMemory&) = delete;
    AlignedMemory& operator=(const AlignedMemory&) = delete;
    
    AlignedMemory(AlignedMemory&& other) noexcept
        : size_(other.size_)
        , ptr_(other.ptr_) {
        other.size_ = 0;
        other.ptr_ = nullptr;
    }
    
    AlignedMemory& operator=(AlignedMemory&& other) noexcept {
        if (this != &other) {
            deallocate();
            size_ = other.size_;
            ptr_ = other.ptr_;
            other.size_ = 0;
            other.ptr_ = nullptr;
        }
        return *this;
    }
    
    // 调整大小
    void resize(size_t new_size) {
        if (new_size == size_) {
            return;
        }
        
        deallocate();
        if (new_size > 0) {
            size_ = new_size;
            ptr_ = allocate(new_size);
        }
    }
    
    // 访问器
    T* data() noexcept { return ptr_; }
    const T* data() const noexcept { return ptr_; }
    
    size_t size() const noexcept { return size_; }
    bool empty() const noexcept { return size_ == 0; }
    
    T& operator[](size_t index) { return ptr_[index]; }
    const T& operator[](size_t index) const { return ptr_[index]; }
    
    T* begin() noexcept { return ptr_; }
    T* end() noexcept { return ptr_ + size_; }
    const T* begin() const noexcept { return ptr_; }
    const T* end() const noexcept { return ptr_ + size_; }

private:
    static T* allocate(size_t size) {
        if (size == 0) {
            return nullptr;
        }
        
        const size_t bytes = size * sizeof(T);
        void* ptr = nullptr;
        
#ifdef _WIN32
        ptr = _aligned_malloc(bytes, Alignment);
#else
        if (posix_memalign(&ptr, Alignment, bytes) != 0) {
            ptr = nullptr;
        }
#endif
        
        if (!ptr) {
            throw std::bad_alloc();
        }
        
        return static_cast<T*>(ptr);
    }
    
    void deallocate() {
        if (ptr_) {
#ifdef _WIN32
            _aligned_free(ptr_);
#else
            free(ptr_);
#endif
            ptr_ = nullptr;
            size_ = 0;
        }
    }

private:
    size_t size_ = 0;
    T* ptr_ = nullptr;
};

// 便捷类型别名
template<typename T>
using AVX2Vector = std::vector<T, AlignedAllocator<T, 32>>;

template<typename T>
using AVX512Vector = std::vector<T, AlignedAllocator<T, 64>>;

template<typename T>
using AVX2Memory = AlignedMemory<T, 32>;

template<typename T>
using AVX512Memory = AlignedMemory<T, 64>;

/**
 * 检查指针是否对齐
 */
template<size_t Alignment>
inline bool is_aligned(const void* ptr) {
    return (reinterpret_cast<uintptr_t>(ptr) & (Alignment - 1)) == 0;
}

} // namespace prophet::simd

