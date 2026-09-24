/*
 * ============================================================================
 * 文件名：immutable_kline.cpp
 * 功能说明：不可变K线数据实现
 * ============================================================================
 */

#include "prophet/parallel/immutable_kline.hpp"
#include <algorithm>

namespace prophet::parallel {

// ============================================================================
// 构造函数
// ============================================================================

ImmutableKlineData::ImmutableKlineData()
    : data_(nullptr)
    , offset_(0)
    , count_(0)
{}

ImmutableKlineData::ImmutableKlineData(
    const std::vector<double>& open,
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume,
    const std::vector<int64_t>& open_time,
    const std::vector<int64_t>& close_time)
    : data_(std::make_shared<const KlineDataImpl>(
          open, high, low, close, volume, open_time, close_time))
    , offset_(0)
    , count_(open.size())
{
    // 验证所有向量长度一致
    if (high.size() != count_ || low.size() != count_ || 
        close.size() != count_ || volume.size() != count_ ||
        open_time.size() != count_ || close_time.size() != count_) {
        throw std::invalid_argument("ImmutableKlineData: All vectors must have the same size");
    }
}

ImmutableKlineData::ImmutableKlineData(
    const double* open,
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    const int64_t* open_time,
    const int64_t* close_time,
    size_t count)
    : data_(std::make_shared<const KlineDataImpl>(
          std::vector<double>(open, open + count),
          std::vector<double>(high, high + count),
          std::vector<double>(low, low + count),
          std::vector<double>(close, close + count),
          std::vector<double>(volume, volume + count),
          std::vector<int64_t>(open_time, open_time + count),
          std::vector<int64_t>(close_time, close_time + count)))
    , offset_(0)
    , count_(count)
{}

ImmutableKlineData::ImmutableKlineData(
    std::shared_ptr<const KlineDataImpl> data,
    size_t offset,
    size_t count)
    : data_(data)
    , offset_(offset)
    , count_(count)
{}

// ============================================================================
// 切片操作
// ============================================================================

ImmutableKlineData ImmutableKlineData::slice(size_t start, size_t count) const {
    if (start + count > count_) {
        throw std::out_of_range("ImmutableKlineData::slice: Index out of range");
    }
    
    return ImmutableKlineData(data_, offset_ + start, count);
}

ImmutableKlineData ImmutableKlineData::recent(size_t count) const {
    if (count > count_) {
        count = count_;
    }
    
    return slice(count_ - count, count);
}

// ============================================================================
// 数据访问（指针）
// ============================================================================

const double* ImmutableKlineData::open_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->open.data() + offset_;
}

const double* ImmutableKlineData::high_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->high.data() + offset_;
}

const double* ImmutableKlineData::low_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->low.data() + offset_;
}

const double* ImmutableKlineData::close_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->close.data() + offset_;
}

const double* ImmutableKlineData::volume_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->volume.data() + offset_;
}

const int64_t* ImmutableKlineData::open_time_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->open_time.data() + offset_;
}

const int64_t* ImmutableKlineData::close_time_data() const {
    if (!data_ || count_ == 0) {
        return nullptr;
    }
    return data_->close_time.data() + offset_;
}

// ============================================================================
// 数据访问（单个元素）
// ============================================================================

double ImmutableKlineData::open(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::open: Index out of range");
    }
    return data_->open[offset_ + index];
}

double ImmutableKlineData::high(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::high: Index out of range");
    }
    return data_->high[offset_ + index];
}

double ImmutableKlineData::low(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::low: Index out of range");
    }
    return data_->low[offset_ + index];
}

double ImmutableKlineData::close(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::close: Index out of range");
    }
    return data_->close[offset_ + index];
}

double ImmutableKlineData::volume(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::volume: Index out of range");
    }
    return data_->volume[offset_ + index];
}

int64_t ImmutableKlineData::open_time(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::open_time: Index out of range");
    }
    return data_->open_time[offset_ + index];
}

int64_t ImmutableKlineData::close_time(size_t index) const {
    if (index >= count_) {
        throw std::out_of_range("ImmutableKlineData::close_time: Index out of range");
    }
    return data_->close_time[offset_ + index];
}

// ============================================================================
// 转换为向量（兼容性）
// ============================================================================

std::vector<double> ImmutableKlineData::open_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<double>(open_data(), open_data() + count_);
}

std::vector<double> ImmutableKlineData::high_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<double>(high_data(), high_data() + count_);
}

std::vector<double> ImmutableKlineData::low_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<double>(low_data(), low_data() + count_);
}

std::vector<double> ImmutableKlineData::close_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<double>(close_data(), close_data() + count_);
}

std::vector<double> ImmutableKlineData::volume_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<double>(volume_data(), volume_data() + count_);
}

std::vector<int64_t> ImmutableKlineData::open_time_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<int64_t>(open_time_data(), open_time_data() + count_);
}

std::vector<int64_t> ImmutableKlineData::close_time_vector() const {
    if (!data_ || count_ == 0) {
        return {};
    }
    return std::vector<int64_t>(close_time_data(), close_time_data() + count_);
}

} // namespace prophet::parallel

