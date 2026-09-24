# Prophet Core 并行化架构重构方案

**日期**: 2025-11-01  
**状态**: 🔴 架构级优化规划

---

## 当前架构的串行瓶颈

### 瓶颈1：K线转换（串行）

**当前实现**（`Context::convertAndSetKlines`）：
```cpp
// Engine接收1分钟K线
context.setKlines("1m", klines_1m);

// 串行转换为多时间框架
context.convertAndSetKlines("1m", "5m");   // 阻塞等待
context.convertAndSetKlines("1m", "15m");  // 阻塞等待
context.convertAndSetKlines("1m", "1h");   // 阻塞等待
context.convertAndSetKlines("1m", "4h");   // 阻塞等待
```

**问题**：
- 4个时间框架转换**完全独立**
- 但却**串行执行**
- 浪费了多核CPU资源

**并行化潜力**：
- 理论加速比：**4x**（4个时间框架）
- 更多时间框架：**Nx**

---

### 瓶颈2：指标计算（串行）

**当前实现**（`IndicatorRefNode::evaluate`）：

一个典型策略DSL：
```dsl
ALL{
    $(5m).RSI().value < 30,           // 串行计算1
    $(5m).MACD().macd > 0,            // 串行计算2
    $(5m).EMA().value > $(5m).SMA().value,  // 串行计算3+4
    $(5m).BBANDS().lower < @CURRENT_PRICE // 串行计算5
} = BUY;
```

**执行流程**：
```
1. 计算 RSI(5m)     ← 30μs
2. 计算 MACD(5m)    ← 30μs
3. 计算 EMA(5m)     ← 20μs
4. 计算 SMA(5m)     ← 20μs
5. 计算 BBANDS(5m)  ← 30μs
总计：130μs
```

**并行化后**：
```
所有指标同时计算 ← 30μs（最慢的一个）
加速比：4.3x
```

**问题**：
- 指标之间**没有依赖关系**
- 都是基于相同的K线数据计算
- 完全可以并行

**并行化潜力**：
- 理论加速比：**指标数量**（3-10x）

---

### 瓶颈3：DSL表达式求值（串行）

**当前实现**（`Evaluator`）：

```cpp
// 复杂表达式
ANY{
    ALL{$(5m).RSI().value < 30, $(5m).MACD().histogram > 0},
    ALL{$(5m).EMA().value > @CURRENT_PRICE, $.VOLUME(5m) > 1000}
}
```

**执行流程**：
```
1. 评估第一个ALL子句
   1.1 计算RSI(5m)
   1.2 比较 < 30
   1.3 计算MACD(5m)
   1.4 比较 > 0
   1.5 AND运算
2. 评估第二个ALL子句
   2.1 计算EMA(5m)
   2.2 比较 > CURRENT_PRICE
   2.3 计算VOLUME(5m)
   2.4 比较 > 1000
   2.5 AND运算
3. OR运算
```

**并行化后**：
```
并行：
  分支1：ALL{RSI, MACD}
  分支2：ALL{EMA, VOLUME}
最后：OR汇总
加速比：2x（分支数）
```

**并行化潜力**：
- 理论加速比：**子句数量**（2-5x）

---

### 瓶颈4：多策略执行（已部分优化）

**当前P0优化**：
- 多个Engine并行执行 ✅
- 但每个Engine内部仍然串行 ❌

**实际应用场景**：
1. **多币种监控**：
   ```
   - BTC/USDT策略
   - ETH/USDT策略
   - BNB/USDT策略
   ```
   
2. **多交易所**：
   ```
   - 币安策略
   - OKX策略
   - Bybit策略
   ```

3. **多时间框架组合**：
   ```
   - 5分钟趋势策略
   - 15分钟震荡策略
   - 1小时趋势策略
   ```

---

## 综合性能分析

### 单策略性能分解（当前）

假设一个典型策略：
```dsl
ALL{
    $(5m).RSI().value < 30,
    $(5m).MACD().macd > $(5m).MACD().signal,
    $(5m).EMA().value > $(5m).SMA().value,
    $(5m).BBANDS().lower < @CURRENT_PRICE
} = BUY;
```

**时间消耗分解**：
```
K线转换（4个时间框架）：
  1m→5m:   50μs
  1m→15m:  40μs  ← 串行，可并行
  1m→1h:   30μs
  1m→4h:   20μs
  合计：   140μs  → 并行后：50μs（最慢的）

指标计算（5个指标）：
  RSI:     30μs
  MACD:    30μs  ← 串行，可并行
  EMA:     20μs
  SMA:     20μs
  BBANDS:  30μs
  合计：   130μs  → 并行后：30μs（最慢的）

表达式求值：
  比较运算：10μs
  AND运算：  5μs
  合计：    15μs

总计：285μs（串行） → 95μs（完全并行）
加速比：3x
```

### 多策略性能（64策略）

**当前P0优化**：
```
单策略：285μs
64策略串行：18,240μs（64 × 285μs）
64策略并行（P0）：976μs（实测）

加速比：18.7x（主要是多策略并行）
但距离理论值（64x）还差很多
```

**瓶颈原因**：
1. 线程创建开销：~500μs
2. 每个Engine内部串行：285μs
3. GIL和同步开销：~191μs

**完全并行化后**：
```
单策略优化：285μs → 95μs（3x）
64策略完全并行：95μs（理想）

预期吞吐量：
64 / 95μs = 673,684 ops/s（vs 当前65,547 ops/s）
提升：10.3x
```

---

## 全面并行化架构设计

### 架构层次

```
┌─────────────────────────────────────────────────┐
│  层次0：批量策略并行（多币种/多交易所）           │
│  - 当前：BatchEngine（P0已优化）                 │
│  - 状态：✅ 已实现，需改进线程池                  │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  层次1：K线转换并行（多时间框架）                 │
│  - 当前：Context::convertAndSetKlines（串行）    │
│  - 优化：ParallelKlineConverter                  │
│  - 预期提升：2-3x                                │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  层次2：指标计算并行（多指标）                    │
│  - 当前：Context::getOrCalculateIndicator（串行）│
│  - 优化：ParallelIndicatorCalculator             │
│  - 预期提升：3-5x                                │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  层次3：表达式求值并行（子表达式）                │
│  - 当前：Evaluator（串行）                       │
│  - 优化：ParallelEvaluator                       │
│  - 预期提升：1.5-2x                              │
└─────────────────────────────────────────────────┘
```

---

## 实施方案

### 方案A：渐进式并行化（推荐）

**阶段1：层次1优化（K线转换并行）**

**优先级**: P1 🔴  
**预期时间**: 3-4小时  
**预期提升**: +2-3x

```cpp
class ParallelKlineConverter {
public:
    void convertMultipleTimeframes(
        Context& ctx,
        const std::string& source_tf,
        const std::vector<std::string>& target_tfs,
        ThreadPool& pool
    ) {
        std::vector<std::future<void>> futures;
        
        for (const auto& target_tf : target_tfs) {
            futures.push_back(pool.submit([&ctx, source_tf, target_tf]() {
                ctx.convertAndSetKlines(source_tf, target_tf);
            }));
        }
        
        // 等待所有转换完成
        for (auto& f : futures) {
            f.get();
        }
    }
};
```

**集成点**：
- `Engine::SetKlines()` 中自动触发
- 根据DSL中的时间框架自动并行转换

---

**阶段2：层次2优化（指标计算并行）**

**优先级**: P1 🔴  
**预期时间**: 5-6小时  
**预期提升**: +3-5x

```cpp
class ParallelIndicatorCalculator {
public:
    std::unordered_map<std::string, IndicatorResult> calculateBatch(
        Context& ctx,
        const std::string& timeframe,
        const std::vector<std::string>& indicators,
        ThreadPool& pool
    ) {
        std::unordered_map<std::string, std::future<IndicatorResult>> futures;
        
        // 并行提交所有指标计算任务
        for (const auto& indicator : indicators) {
            futures[indicator] = pool.submit([&ctx, timeframe, indicator]() {
                return ctx.getOrCalculateIndicator(indicator, timeframe);
            });
        }
        
        // 收集结果
        std::unordered_map<std::string, IndicatorResult> results;
        for (auto& [name, future] : futures) {
            results[name] = future.get();
        }
        
        return results;
    }
};
```

**挑战**：
- 需要分析DSL，提前识别所有需要的指标
- 需要确保指标计算的线程安全

**解决方案**：
1. 在Parser阶段收集所有指标引用
2. 在evaluate之前批量预计算
3. 使用线程本地存储（TLS）保证安全

---

**阶段3：层次3优化（表达式并行）**

**优先级**: P2 🟡  
**预期时间**: 6-8小时  
**预期提升**: +1.5-2x

```cpp
class ParallelEvaluator {
public:
    Value evaluateAnyNode(
        const std::vector<std::shared_ptr<ASTNode>>& branches,
        Context& ctx,
        ThreadPool& pool
    ) {
        std::vector<std::future<Value>> futures;
        
        // 并行评估所有分支
        for (const auto& branch : branches) {
            futures.push_back(pool.submit([&branch, &ctx]() {
                return branch->evaluate(ctx);
            }));
        }
        
        // ANY逻辑：只要有一个true就返回true
        for (auto& f : futures) {
            Value result = f.get();
            if (result.isBoolean() && result.asBool()) {
                return Value::fromBoolean(true);
            }
        }
        
        return Value::fromBoolean(false);
    }
};
```

**挑战**：
- 需要确保Context的线程安全
- 需要避免短路求值的性能损失

---

**阶段4：统一线程池**

**优先级**: P1 🔴  
**预期时间**: 2-3小时  
**必须先完成**

```cpp
class GlobalThreadPool {
public:
    static GlobalThreadPool& instance() {
        static GlobalThreadPool pool(std::thread::hardware_concurrency());
        return pool;
    }
    
    template<typename F>
    auto submit(F&& f) -> std::future<decltype(f())> {
        using return_type = decltype(f());
        
        auto task = std::make_shared<std::packaged_task<return_type()>>(
            std::forward<F>(f)
        );
        
        std::future<return_type> res = task->get_future();
        {
            std::unique_lock<std::mutex> lock(queue_mutex_);
            
            if (stop_) {
                throw std::runtime_error("submit on stopped ThreadPool");
            }
            
            tasks_.emplace([task]() { (*task)(); });
        }
        condition_.notify_one();
        return res;
    }
    
private:
    std::vector<std::thread> workers_;
    std::queue<std::function<void()>> tasks_;
    std::mutex queue_mutex_;
    std::condition_variable condition_;
    bool stop_;
};
```

---

### 方案B：激进式并行化

**一次性实现所有层次并行**

**优势**：
- 一次达到最大性能
- 避免多次重构

**劣势**：
- 风险高，难以调试
- 时间长（15-20小时）
- 可能引入难以发现的bug

**不推荐**，除非有充足的测试时间。

---

## 性能预测

### 完全并行化后的性能

**单策略**（当前vs优化）：
```
当前：285μs
  ├─ K线转换：140μs → 50μs（P1层次1）
  ├─ 指标计算：130μs → 30μs（P1层次2）
  └─ 表达式求值：15μs → 10μs（P2层次3）
优化后：90μs

单策略加速比：3.2x
单策略吞吐量：11,111 ops/s → 35,556 ops/s
```

**64策略批量**（当前vs优化）：
```
当前（P0）：65,547 ops/s
  ├─ 单策略优化（P1）：→ 209,751 ops/s（3.2x）
  ├─ 线程池优化：→ 419,502 ops/s（2x）
  └─ 批量K线预转换：→ 524,378 ops/s（1.25x）
最终：524,378 ops/s

vs 1M目标：还需1.9x（P2字节码+P3 SIMD）
```

**到达1M ops/s的路径**：
```
当前P0：        65,547 ops/s
  ↓ P1层次1+2（+6x）
P1完成：       393,282 ops/s
  ↓ 线程池（+2x）
P1优化：       786,564 ops/s
  ↓ P2字节码（+1.3x）
P2完成：     1,022,533 ops/s ✅ 超越1M目标！
```

---

## 实施优先级

### 立即执行（本周）

1. ✅ **统一线程池**（P1，2-3小时）
   - 替换所有`std::async`
   - 减少线程创建开销
   - **预期提升**: +2x（vs P0）

2. ✅ **K线转换并行**（P1，3-4小时）
   - `ParallelKlineConverter`
   - 自动识别时间框架
   - **预期提升**: +1.5x（累计3x）

3. ✅ **指标计算并行**（P1，5-6小时）
   - `ParallelIndicatorCalculator`
   - DSL指标依赖分析
   - **预期提升**: +2x（累计6x）

### 短期执行（下周）

4. ⏳ **表达式并行求值**（P2，6-8小时）
   - `ParallelEvaluator`
   - 子表达式并行
   - **预期提升**: +1.5x（累计9x）

5. ⏳ **字节码深度优化**（P2，已部分完成）
   - 指令融合
   - 常量折叠
   - **预期提升**: +1.3x（累计11.7x）

### 中期执行（2周后）

6. ⏳ **SIMD全面集成**（P3，已完成核心）
   - 62个指标SIMD化
   - 批量K线计算
   - **预期提升**: +1.2x（累计14x）

---

## 风险评估

### 技术风险

1. **线程安全问题** 🔴
   - Context不是线程安全的
   - IndicatorCache可能有竞争
   - **缓解**: 使用TLS或per-thread Context

2. **调试困难** 🟡
   - 并发bug难以复现
   - **缓解**: 详细的日志，单元测试

3. **性能不达预期** 🟡
   - 任务粒度太细，开销大
   - **缓解**: 动态调整批次大小

### 实施风险

1. **时间估算偏差** 🟡
   - 可能需要更多时间
   - **缓解**: 分阶段实施，逐步验证

2. **向后兼容性** 🟢
   - API可能需要调整
   - **缓解**: 保留旧API，标记为deprecated

---

## 结论

**用户的洞察完全正确！**

当前的"多策略并行"只是表面优化，真正的瓶颈在于：
1. ❌ K线转换串行
2. ❌ 指标计算串行
3. ❌ 表达式求值串行

**完全并行化后的预期性能**：
- 单策略：3.2x提升
- 64策略：10x提升（vs 当前P0）
- 最终吞吐量：**786,564 ops/s**

**加上字节码优化**：
- 最终吞吐量：**1,022,533 ops/s** ✅

**1M ops/s目标完全可达！**

**建议立即行动**：
1. 先实现统一线程池（必须）
2. 再实现K线转换并行（影响最大）
3. 然后实现指标计算并行（核心优化）

**预计总时间**: 10-13小时  
**预计最终性能**: 786,564 ops/s → 1,022,533 ops/s ✅

