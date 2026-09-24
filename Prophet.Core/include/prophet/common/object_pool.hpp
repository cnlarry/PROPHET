/*
 * ============================================================================
 * 文件名：object_pool.hpp
 * 功能说明：通用对象池（阶段2优化）
 * 
 * 性能优化：
 * - 减少内存分配次数
 * - 零拷贝：使用移动语义
 * - 线程安全：支持多线程场景
 * ============================================================================
 */

#pragma once

#include <vector>
#include <mutex>
#include <memory>
#include <cstddef>

namespace prophet::utils {

/**
 * 通用对象池
 * 
 * 用于频繁创建/销毁的对象，避免重复内存分配
 * 
 * @tparam T 对象类型
 */
template<typename T>
class ObjectPool {
public:
    /**
     * RAII管理器，自动归还对象到池中
     */
    class PooledObject {
    public:
        PooledObject() = default;
        
        PooledObject(T&& obj, ObjectPool<T>* pool)
            : object_(std::make_unique<T>(std::move(obj)))
            , pool_(pool) {}
        
        ~PooledObject() {
            if (pool_ && object_) {
                pool_->release(std::move(*object_));
            }
        }
        
        // 禁止拷贝，允许移动
        PooledObject(const PooledObject&) = delete;
        PooledObject& operator=(const PooledObject&) = delete;
        
        PooledObject(PooledObject&& other) noexcept
            : object_(std::move(other.object_))
            , pool_(other.pool_) {
            other.pool_ = nullptr;
        }
        
        PooledObject& operator=(PooledObject&& other) noexcept {
            if (this != &other) {
                if (pool_ && object_) {
                    pool_->release(std::move(*object_));
                }
                object_ = std::move(other.object_);
                pool_ = other.pool_;
                other.pool_ = nullptr;
            }
            return *this;
        }
        
        T& get() { return *object_; }
        const T& get() const { return *object_; }
        
        T* operator->() { return object_.get(); }
        const T* operator->() const { return object_.get(); }
        
        T& operator*() { return *object_; }
        const T& operator*() const { return *object_; }
        
    private:
        std::unique_ptr<T> object_;
        ObjectPool<T>* pool_ = nullptr;
    };

public:
    /**
     * 构造函数
     * @param initial_size 初始池大小
     * @param max_size 最大池大小（0=无限制）
     */
    explicit ObjectPool(size_t initial_size = 8, size_t max_size = 128)
        : max_size_(max_size) {
        pool_.reserve(initial_size);
    }
    
    ~ObjectPool() = default;
    
    // 禁止拷贝和移动
    ObjectPool(const ObjectPool&) = delete;
    ObjectPool& operator=(const ObjectPool&) = delete;
    ObjectPool(ObjectPool&&) = delete;
    ObjectPool& operator=(ObjectPool&&) = delete;
    
    /**
     * 获取对象（从池中或创建新对象）
     */
    PooledObject acquire() {
        std::lock_guard<std::mutex> lock(mutex_);
        
        if (!pool_.empty()) {
            T obj = std::move(pool_.back());
            pool_.pop_back();
            return PooledObject(std::move(obj), this);
        }
        
        // 池为空，创建新对象
        return PooledObject(T{}, this);
    }
    
    /**
     * 释放对象回池中
     */
    void release(T&& obj) {
        std::lock_guard<std::mutex> lock(mutex_);
        
        // 清空对象数据（如果T有clear方法）
        if constexpr (requires { obj.clear(); }) {
            obj.clear();
        }
        
        // 检查池大小限制
        if (max_size_ == 0 || pool_.size() < max_size_) {
            pool_.push_back(std::move(obj));
        }
        // 如果超过限制，对象自动销毁
    }
    
    /**
     * 清空对象池
     */
    void clear() {
        std::lock_guard<std::mutex> lock(mutex_);
        pool_.clear();
    }
    
    /**
     * 获取池中对象数量
     */
    size_t size() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return pool_.size();
    }

private:
    std::vector<T> pool_;
    size_t max_size_;
    mutable std::mutex mutex_;
};

/**
 * std::vector特化版本
 */
template<typename T>
class VectorPool {
public:
    using Vector = std::vector<T>;
    
    class PooledVector {
    public:
        PooledVector() = default;
        
        PooledVector(Vector&& vec, VectorPool<T>* pool)
            : vector_(std::make_unique<Vector>(std::move(vec)))
            , pool_(pool) {}
        
        ~PooledVector() {
            if (pool_ && vector_) {
                pool_->release(std::move(*vector_));
            }
        }
        
        // 禁止拷贝，允许移动
        PooledVector(const PooledVector&) = delete;
        PooledVector& operator=(const PooledVector&) = delete;
        
        PooledVector(PooledVector&& other) noexcept
            : vector_(std::move(other.vector_))
            , pool_(other.pool_) {
            other.pool_ = nullptr;
        }
        
        PooledVector& operator=(PooledVector&& other) noexcept {
            if (this != &other) {
                if (pool_ && vector_) {
                    pool_->release(std::move(*vector_));
                }
                vector_ = std::move(other.vector_);
                pool_ = other.pool_;
                other.pool_ = nullptr;
            }
            return *this;
        }
        
        Vector& get() { return *vector_; }
        const Vector& get() const { return *vector_; }
        
        Vector* operator->() { return vector_.get(); }
        const Vector* operator->() const { return vector_.get(); }
        
        Vector& operator*() { return *vector_; }
        const Vector& operator*() const { return *vector_; }
        
    private:
        std::unique_ptr<Vector> vector_;
        VectorPool<T>* pool_ = nullptr;
    };

public:
    explicit VectorPool(size_t initial_size = 8, size_t max_size = 64)
        : max_size_(max_size) {
        pool_.reserve(initial_size);
    }
    
    PooledVector acquire() {
        std::lock_guard<std::mutex> lock(mutex_);
        
        if (!pool_.empty()) {
            Vector vec = std::move(pool_.back());
            pool_.pop_back();
            return PooledVector(std::move(vec), this);
        }
        
        return PooledVector(Vector{}, this);
    }
    
    void release(Vector&& vec) {
        std::lock_guard<std::mutex> lock(mutex_);
        
        vec.clear();  // 清空内容但保留capacity
        
        if (max_size_ == 0 || pool_.size() < max_size_) {
            pool_.push_back(std::move(vec));
        }
    }
    
    void clear() {
        std::lock_guard<std::mutex> lock(mutex_);
        pool_.clear();
    }
    
    size_t size() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return pool_.size();
    }

private:
    std::vector<Vector> pool_;
    size_t max_size_;
    mutable std::mutex mutex_;
};

} // namespace prophet::utils

