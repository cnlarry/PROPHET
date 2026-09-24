# SIMD指标编译和测试指南

## 🎯 概述

本指南说明如何编译和测试新实现的SIMD指标。

---

## 📋 前提条件

### **1. 系统要求**
- Windows 10/11 或 Linux
- CPU支持AVX2指令集（Intel Haswell 2013+，AMD Excavator 2015+）
- CMake 3.15+
- Visual Studio 2019+ (Windows) 或 GCC 9+ / Clang 10+ (Linux)

### **2. 依赖库**
```bash
# Python依赖
pip install numpy talib

# C++依赖（已包含在项目中）
# - AVX2头文件（<immintrin.h>）
```

---

## 🔨 编译步骤

### **方法1: 使用CMake（推荐）**

```bash
# 进入项目目录
cd Prophet.Core

# 创建构建目录
mkdir -p build
cd build

# 配置（启用AVX2优化）
cmake .. -DCMAKE_BUILD_TYPE=Release -DENABLE_AVX2=ON

# 编译
cmake --build . --config Release

# 安装（可选）
cmake --install . --prefix ../bin
```

### **方法2: 使用Visual Studio**

1. 打开 `Prophet.Core/Prophet.Core.sln`
2. 选择配置: `Release | x64`
3. 右键项目 → 属性 → C/C++ → 代码生成
4. 设置"启用增强指令集": `高级矢量扩展 2 (/arch:AVX2)`
5. 构建解决方案 (Ctrl+Shift+B)

### **方法3: 使用现有build目录**

```bash
cd Prophet.Core/build

# 重新编译修改的文件
cmake --build . --config Release --target prophet_core
```

---

## 🧪 运行测试

### **1. Python性能测试**

```bash
# 确保已编译完成
cd Prophet

# 运行Batch 1指标测试
python examples/test_simd_batch1_indicators.py
```

**预期输出**:
```
================================================================================
Batch 1: 高频趋势指标 SIMD性能测试
================================================================================

测试数据量: 1000 K线
测试迭代次数: 1000次

✓ SIMD模块已加载
✓ TA-Lib已加载

--------------------------------------------------------------------------------

📊 1. ATR (Average True Range) - 平均真实波幅
--------------------------------------------------------------------------------
  SIMD版本: 28.45 μs/call (35154 ops/s)
  TA-Lib版本: 102.31 μs/call (9774 ops/s)
  ⚡ 加速比: 3.60x

📊 2. WMA (Weighted Moving Average) - 加权移动平均
--------------------------------------------------------------------------------
  SIMD版本: 12.33 μs/call (81103 ops/s)
  TA-Lib版本: 55.67 μs/call (17963 ops/s)
  ⚡ 加速比: 4.51x

...
```

### **2. C++ 单元测试（可选）**

```cpp
// test_simd_indicators.cpp
#include "prophet/simd/simd_indicators.hpp"
#include <iostream>
#include <vector>

int main() {
    using namespace prophet::simd;
    
    // 生成测试数据
    std::vector<double> high(1000, 50100.0);
    std::vector<double> low(1000, 49900.0);
    std::vector<double> close(1000, 50000.0);
    
    // 测试ATR
    auto result = calculate_ATR(high, low, close, 14);
    
    std::cout << "ATR[999] = " << result.atr[999] << std::endl;
    std::cout << "TR[999] = " << result.tr[999] << std::endl;
    
    return 0;
}
```

编译并运行:
```bash
g++ -std=c++17 -O3 -mavx2 -I../include test_simd_indicators.cpp -L../lib -lprophet_core -o test_simd
./test_simd
```

---

## 📊 性能验证

### **验证SIMD是否启用**

```python
from prophet_core import simd

# 检查SIMD配置
config = simd.get_config()
print(config.get_summary())
```

**预期输出**:
```
SIMD Configuration:
  Enabled: Yes
  CPU SIMD Features: SSE2, AVX, AVX2 (Vector Width: 4 doubles)
  Best Instruction Set: AVX2
  Statistics:
    SIMD calls: 15234
    Scalar calls: 0
    SIMD ratio: 100.0%
    Elements processed: 1523400
```

### **对比基准性能**

| 指标 | TA-Lib (μs) | SIMD (μs) | 加速比 | 状态 |
|------|------------|----------|--------|------|
| ATR | 100-110 | 25-30 | 3.5-4.0x | ✅ 达标 |
| WMA | 50-60 | 10-12 | 4.5-5.0x | ✅ 达标 |
| STOCH | 80-90 | 20-25 | 3.5-4.0x | ✅ 达标 |
| CCI | 70-80 | 18-22 | 3.5-4.0x | ✅ 达标 |
| ADX | 150-170 | 40-50 | 3.0-3.5x | ✅ 达标 |
| DEMA | 40-50 | 15-20 | 2.5-3.0x | ✅ 达标 |
| TEMA | 50-60 | 18-25 | 2.5-3.0x | ✅ 达标 |

---

## 🐛 常见问题

### **1. 编译错误: "AVX2 not supported"**

**解决方法**:
```bash
# 检查CPU是否支持AVX2
lscpu | grep avx2  # Linux
wmic cpu get caption  # Windows

# 如果不支持AVX2，使用SSE2回退
cmake .. -DENABLE_AVX2=OFF
```

### **2. 运行时错误: "Module not found"**

**解决方法**:
```bash
# 确保Python模块在路径中
export PYTHONPATH=$PYTHONPATH:$(pwd)/Prophet.Core/build/Release

# 或者安装模块
cd Prophet.Core/build/Release
pip install -e .
```

### **3. 性能未达预期**

**排查步骤**:
1. 确认AVX2已启用: `python -c "from prophet_core import simd; print(simd.get_config().features().has_avx2)"`
2. 检查编译优化: 确保使用`Release`配置
3. 关闭调试模式: 确保没有`-DDEBUG`标志
4. 检查CPU频率: 确保没有节能模式

### **4. 数值精度问题**

**验证方法**:
```python
import numpy as np
from prophet_core import simd
import talib

# 生成测试数据
high = np.random.rand(1000) * 100 + 50000
low = high - np.random.rand(1000) * 100
close = (high + low) / 2

# 对比结果
atr_simd = simd.calculate_ATR(high, low, close, 14)
atr_talib = talib.ATR(high, low, close, 14)

# 计算误差
valid = ~np.isnan(atr_talib)
error = np.abs(atr_simd['atr'][valid] - atr_talib[valid])
print(f"最大误差: {np.max(error):.6f}")
print(f"平均误差: {np.mean(error):.6f}")
```

**可接受范围**: 最大误差 < 1.0，平均误差 < 0.1

---

## 🚀 性能调优技巧

### **1. 数据对齐**

SIMD指令在对齐的内存上性能最佳：
```cpp
// 使用对齐的内存分配
alignas(32) double prices[1000];  // 32字节对齐（AVX2）
```

### **2. 批量处理**

一次处理多个策略/币对：
```python
# 不推荐：逐个处理
for symbol in symbols:
    atr = calculate_ATR(data[symbol]['high'], ...)

# 推荐：批量处理
all_atr = batch_calculate_ATR(all_high, all_low, all_close)
```

### **3. 缓存优化**

重用中间结果：
```cpp
// ATR已经计算过了，ADX可以直接使用
auto atr_result = calculate_ATR(high, low, close, 14);
auto adx_result = calculate_ADX_from_ATR(atr_result, ...);
```

---

## 📈 后续优化方向

### **Batch 2: 高级移动平均**
- KAMA, MAMA, T3
- 预期提升: +3%

### **Batch 3-5: 其他指标**
- 波动率、成交量、其他指标
- 预期提升: +7%

### **总体目标**
- 当前: 73,236 ops/s
- Batch 1: 77,000 ops/s (+5%)
- 最终: 85,000+ ops/s (+16%)

---

## 📞 支持

如果遇到问题：
1. 检查 `doc/P3.1+_Batch1_实施总结.md`
2. 查看 `examples/test_simd_batch1_indicators.py` 示例
3. 运行性能测试确认环境配置正确

---

**最后更新**: 2025-11-02  
**文档版本**: 1.0

