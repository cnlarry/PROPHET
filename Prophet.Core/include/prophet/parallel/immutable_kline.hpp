/*
 * ============================================================================
 * 文件名：immutable_kline.hpp
 * 功能说明：不可变K线数据 - 零拷贝、线程安全
 * 
 * 设计理念：
 * - 不可变（Immutable）：一旦创建，数据不可修改
 * - 引用计数（shared_ptr）：多线程安全共享
 * - 零拷贝切片：支持高效的数据视图
 * - 值语义：可安全拷贝、传递
 * 
 * 优势：
 * - 完全线程安全（无需加锁）
 * - 高效内存利用（引用计数+结构化共享）
 * - 简化API（无需担心所有权）
 * ============================================================================
 */

#pragma once

#include <vector>
#include <memory>
#include <cstdint>
#include <stdexcept>

namespace prophet::parallel {

/**
 * 不可变K线数据
 * 
 * 特性：
 * - 不可变：所有访问都是const
 * - 零拷贝：内部使用shared_ptr，拷贝只增加引用计数
 * - 线程安全：可以安全地在多线程间共享
 * - 切片视图：支持高效的子序列访问
 * 
 * 使用示例：
 *   // 创建
 *   ImmutableKlineData klines(open, high, low, close, volume, open_time, close_time);
 *   
 *   // 安全共享（零拷贝）
 *   auto klines_copy = klines;
 *   
 *   // 切片（零拷贝）
 *   auto recent_100 = klines.slice(klines.size() - 100, 100);
 */
class ImmutableKlineData {
public:
    /**
     * 默认构造（空数据）
     */
    ImmutableKlineData();
    
    /**
     * 从向量构造（拷贝数据）
     */
    ImmutableKlineData(const std::vector<double>& open,
                       const std::vector<double>& high,
                       const std::vector<double>& low,
                       const std::vector<double>& close,
                       const std::vector<double>& volume,
                       const std::vector<int64_t>& open_time,
                       const std::vector<int64_t>& close_time);
    
    /**
     * 从裸指针构造（拷贝数据）
     */
    ImmutableKlineData(const double* open,
                       const double* high,
                       const double* low,
                       const double* close,
                       const double* volume,
                       const int64_t* open_time,
                       const int64_t* close_time,
                       size_t count);
    
    // 默认拷贝/移动（引用计数，零拷贝）
    ImmutableKlineData(const ImmutableKlineData&) = default;
    ImmutableKlineData(ImmutableKlineData&&) noexcept = default;
    ImmutableKlineData& operator=(const ImmutableKlineData&) = default;
    ImmutableKlineData& operator=(ImmutableKlineData&&) noexcept = default;
    
    /**
     * 切片（零拷贝视图）
     * @param start 起始索引（相对于原始数据）
     * @param count 数量
     * @return 新的ImmutableKlineData（共享底层数据）
     */
    ImmutableKlineData slice(size_t start, size_t count) const;
    
    /**
     * 获取最近N根K线（零拷贝）
     */
    ImmutableKlineData recent(size_t count) const;
    
    // ========================================================================
    // 数据访问（所有方法都是const）
    // ========================================================================
    
    /**
     * 获取K线数量
     */
    size_t size() const { return count_; }
    
    /**
     * 是否为空
     */
    bool empty() const { return count_ == 0; }
    
    /**
     * 获取开盘价数组（只读）
     */
    const double* open_data() const;
    
    /**
     * 获取最高价数组（只读）
     */
    const double* high_data() const;
    
    /**
     * 获取最低价数组（只读）
     */
    const double* low_data() const;
    
    /**
     * 获取收盘价数组（只读）
     */
    const double* close_data() const;
    
    /**
     * 获取成交量数组（只读）
     */
    const double* volume_data() const;
    
    /**
     * 获取开盘时间数组（只读）
     */
    const int64_t* open_time_data() const;
    
    /**
     * 获取收盘时间数组（只读）
     */
    const int64_t* close_time_data() const;
    
    /**
     * 获取单根K线的开盘价
     */
    double open(size_t index) const;
    
    /**
     * 获取单根K线的最高价
     */
    double high(size_t index) const;
    
    /**
     * 获取单根K线的最低价
     */
    double low(size_t index) const;
    
    /**
     * 获取单根K线的收盘价
     */
    double close(size_t index) const;
    
    /**
     * 获取单根K线的成交量
     */
    double volume(size_t index) const;
    
    /**
     * 获取单根K线的开盘时间
     */
    int64_t open_time(size_t index) const;
    
    /**
     * 获取单根K线的收盘时间
     */
    int64_t close_time(size_t index) const;
    
    /**
     * 转换为向量（用于与旧代码兼容）
     */
    std::vector<double> open_vector() const;
    std::vector<double> high_vector() const;
    std::vector<double> low_vector() const;
    std::vector<double> close_vector() const;
    std::vector<double> volume_vector() const;
    std::vector<int64_t> open_time_vector() const;
    std::vector<int64_t> close_time_vector() const;
    
private:
    /**
     * 内部数据结构（不可变）
     */
    struct KlineDataImpl {
        std::vector<double> open;
        std::vector<double> high;
        std::vector<double> low;
        std::vector<double> close;
        std::vector<double> volume;
        std::vector<int64_t> open_time;
        std::vector<int64_t> close_time;
        
        KlineDataImpl() = default;
        
        KlineDataImpl(const std::vector<double>& o,
                     const std::vector<double>& h,
                     const std::vector<double>& l,
                     const std::vector<double>& c,
                     const std::vector<double>& v,
                     const std::vector<int64_t>& ot,
                     const std::vector<int64_t>& ct)
            : open(o), high(h), low(l), close(c)
            , volume(v), open_time(ot), close_time(ct)
        {}
    };
    
    /**
     * 私有构造（用于切片）
     */
    ImmutableKlineData(std::shared_ptr<const KlineDataImpl> data,
                       size_t offset,
                       size_t count);
    
    // 共享的底层数据（不可变）
    std::shared_ptr<const KlineDataImpl> data_;
    
    // 视图范围
    size_t offset_;  // 起始偏移
    size_t count_;   // 数量
};

} // namespace prophet::parallel

