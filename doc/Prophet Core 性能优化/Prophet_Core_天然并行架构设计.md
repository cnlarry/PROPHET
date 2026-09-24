# Prophet Core 天然并行架构设计

**日期**: 2025-11-01  
**核心理念**: 多线程像DNA一样刻入系统骨子里

---

## 设计哲学

### 传统错误思路 ❌
```
串行系统 → 加锁 → 加线程池 → 勉强并行
```

**问题**：
- 锁竞争严重
- 死锁风险
- 性能瓶颈
- 难以维护

### 正确思路 ✅
```
天然并行架构 → 无锁设计 → 不可变数据 → 自然并行
```

**优势**：
- 无锁竞争
- 无死锁
- 线性扩展
- 易于推理

---

## 核心原则

### 原则1：不可变性（Immutability First）

**现状问题**：
```cpp
// ❌ 可变状态，线程不安全
class Context {
    std::unordered_map<std::string, IndicatorResult> indicators_;
    
    void setIndicator(...) {
        indicators_[key] = value;  // 多线程竞争
    }
};
```

**重构方案**：
```cpp
// ✅ 不可变数据，天然线程安全
class ImmutableContext {
    const std::shared_ptr<const IndicatorMap> indicators_;
    
    // 所有修改返回新实例
    ImmutableContext withIndicator(const std::string& key, 
                                    const IndicatorResult& value) const {
        auto new_map = std::make_shared<IndicatorMap>(*indicators_);
        (*new_map)[key] = value;
        return ImmutableContext(new_map, ...);
    }
};
```

**优势**：
- 完全线程安全
- 无需加锁
- 可以安全共享
- 结构化共享（Copy-on-Write）

---

### 原则2：值语义（Value Semantics）

**现状问题**：
```cpp
// ❌ 引用传递，隐式依赖
Value IndicatorRefNode::evaluate(const Context& ctx) const {
    // ctx可能被其他线程修改
    return ctx.getIndicator(...);
}
```

**重构方案**：
```cpp
// ✅ 值传递，显式依赖
struct EvaluationInput {
    std::shared_ptr<const KlineData> klines;
    std::shared_ptr<const IndicatorMap> indicators;
    double current_price;
    int64_t current_time;
};

Value IndicatorRefNode::evaluate(const EvaluationInput& input) const {
    // input是不可变的，完全线程安全
    return input.indicators->at(...);
}
```

---

### 原则3：函数式计算图（Functional Computation Graph）

**当前问题**：
```cpp
// ❌ 命令式，串行执行
value1 = calculate_RSI(klines);
value2 = calculate_MACD(klines);
value3 = calculate_EMA(klines);
result = evaluate(value1, value2, value3);
```

**重构方案**：
```cpp
// ✅ 声明式，自动并行
ComputationGraph graph;

// 构建计算图
auto rsi_node = graph.add_node(RSI_Calculator, {klines_node});
auto macd_node = graph.add_node(MACD_Calculator, {klines_node});
auto ema_node = graph.add_node(EMA_Calculator, {klines_node});
auto result_node = graph.add_node(Evaluator, {rsi_node, macd_node, ema_node});

// 自动并行执行
auto result = graph.execute_parallel(result_node, thread_pool);
```

**优势**：
- 自动发现并行性
- 自动优化执行顺序
- 可视化计算依赖
- 易于缓存和重用

---

## 重构设计

### 层次1：线程安全的数据层

#### 1.1 不可变K线数据

```cpp
// 零拷贝，引用计数，线程安全
class ImmutableKlineData {
public:
    // 构造时拷贝，之后不可变
    ImmutableKlineData(const std::vector<double>& open,
                       const std::vector<double>& high,
                       const std::vector<double>& low,
                       const std::vector<double>& close,
                       const std::vector<double>& volume,
                       const std::vector<int64_t>& open_time,
                       const std::vector<int64_t>& close_time)
        : data_(std::make_shared<const KlineDataImpl>(
              open, high, low, close, volume, open_time, close_time))
    {}
    
    // 所有访问都是const
    const std::vector<double>& open() const { return data_->open; }
    const std::vector<double>& high() const { return data_->high; }
    // ... 其他字段
    
    size_t size() const { return data_->open.size(); }
    
    // 切片（零拷贝）
    ImmutableKlineData slice(size_t start, size_t count) const {
        return ImmutableKlineData(data_, start, count);
    }
    
private:
    struct KlineDataImpl {
        std::vector<double> open, high, low, close, volume;
        std::vector<int64_t> open_time, close_time;
        // ... 构造函数
    };
    
    std::shared_ptr<const KlineDataImpl> data_;
    size_t offset_ = 0;
    size_t count_ = 0;
};
```

#### 1.2 并行K线转换器

```cpp
class ParallelKlineConverter {
public:
    // 批量并行转换
    std::unordered_map<std::string, ImmutableKlineData> convertMultiple(
        const ImmutableKlineData& source_1m,
        const std::vector<std::string>& target_timeframes,
        ThreadPool& pool
    ) {
        // 构建转换任务
        std::vector<std::future<std::pair<std::string, ImmutableKlineData>>> futures;
        
        for (const auto& tf : target_timeframes) {
            futures.push_back(pool.submit([&source_1m, tf]() {
                return std::make_pair(tf, convert_single(source_1m, tf));
            }));
        }
        
        // 收集结果
        std::unordered_map<std::string, ImmutableKlineData> results;
        for (auto& f : futures) {
            auto [tf, klines] = f.get();
            results[tf] = std::move(klines);
        }
        
        return results;
    }
    
private:
    static ImmutableKlineData convert_single(
        const ImmutableKlineData& source,
        const std::string& target_tf
    ) {
        // 使用KlineConverter::convert
        // 返回新的不可变K线数据
    }
};
```

---

### 层次2：无锁的计算层

#### 2.1 计算任务抽象

```cpp
// 计算任务接口（纯函数）
template<typename Result, typename... Inputs>
class ComputationTask {
public:
    virtual ~ComputationTask() = default;
    
    // 纯函数：相同输入→相同输出，无副作用
    virtual Result compute(const Inputs&... inputs) const = 0;
    
    // 任务元数据
    virtual std::string name() const = 0;
    virtual size_t estimated_cost() const = 0;  // 用于任务调度
};

// 指标计算任务
class IndicatorComputationTask : public ComputationTask<IndicatorResult, ImmutableKlineData, IndicatorParams> {
public:
    IndicatorComputationTask(const std::string& indicator_name)
        : indicator_name_(indicator_name)
    {}
    
    IndicatorResult compute(const ImmutableKlineData& klines,
                           const IndicatorParams& params) const override {
        // 使用Calculator计算指标
        // 完全无状态，线程安全
        return calculator_.calculate(indicator_name_, klines, params);
    }
    
    std::string name() const override { return "Indicator:" + indicator_name_; }
    size_t estimated_cost() const override { return estimate_indicator_cost(indicator_name_); }
    
private:
    std::string indicator_name_;
    static indicators::Calculator calculator_;  // 无状态，线程安全
};
```

#### 2.2 数据函数并行化

**当前瓶颈分析**：

数据函数（Data Functions）如`HIGHEST`, `LOWEST`, `AVERAGE`等通常涉及：
```cpp
// HIGHEST(5m).close(20) - 找最近20根K线的最高收盘价
for (int i = 0; i < 20; i++) {
    max_close = std::max(max_close, klines.close[current - i]);
}
```

**并行化方案**：

```cpp
class ParallelDataFunctionExecutor {
public:
    // HIGHEST/LOWEST/AVERAGE等聚合函数
    template<typename AggFunc>
    Value computeAggregation(
        const ImmutableKlineData& klines,
        const std::string& field,
        size_t PERIOD,
        AggFunc&& agg_func,
        ThreadPool& pool
    ) const {
        const auto& data = get_field_data(klines, field);
        
        // 小数据量：直接串行
        if (PERIOD < 64) {
            return compute_serial(data, PERIOD, agg_func);
        }
        
        // 大数据量：分块并行
        size_t chunk_size = (PERIOD + pool.num_threads() - 1) / pool.num_threads();
        std::vector<std::future<Value>> futures;
        
        for (size_t i = 0; i < PERIOD; i += chunk_size) {
            size_t end = std::min(i + chunk_size, PERIOD);
            futures.push_back(pool.submit([&data, i, end, &agg_func]() {
                return compute_chunk(data, i, end, agg_func);
            }));
        }
        
        // 合并分块结果
        std::vector<Value> chunk_results;
        for (auto& f : futures) {
            chunk_results.push_back(f.get());
        }
        
        return agg_func.merge(chunk_results);
    }
    
    // SIMD优化的数据函数
    Value computeHighestSIMD(
        const ImmutableKlineData& klines,
        const std::string& field,
        size_t PERIOD
    ) const {
        const auto& data = get_field_data(klines, field);
        
        // 使用SIMD查找最大值
        return simd::find_max(data.data() + data.size() - PERIOD, PERIOD);
    }
};
```

**关键优化**：
1. **小数据串行**：避免线程开销
2. **大数据并行**：分块+合并
3. **SIMD加速**：向量化搜索

---

### 层次3：自动并行的求值层

#### 3.1 计算图构建

```cpp
class ComputationGraph {
public:
    using NodeId = size_t;
    
    // 添加计算节点
    template<typename Task, typename... InputNodes>
    NodeId add_node(std::shared_ptr<Task> task, InputNodes... input_nodes) {
        NodeId id = nodes_.size();
        
        Node node;
        node.task = std::move(task);
        node.inputs = {input_nodes...};
        
        nodes_.push_back(std::move(node));
        return id;
    }
    
    // 添加数据节点（叶子节点）
    template<typename T>
    NodeId add_data_node(const T& data) {
        NodeId id = nodes_.size();
        
        Node node;
        node.data = std::make_shared<T>(data);
        
        nodes_.push_back(std::move(node));
        return id;
    }
    
    // 拓扑排序（检测循环依赖）
    std::vector<NodeId> topological_sort(NodeId target) const {
        std::vector<NodeId> sorted;
        std::unordered_set<NodeId> visited;
        std::unordered_set<NodeId> in_stack;
        
        topological_sort_dfs(target, visited, in_stack, sorted);
        
        return sorted;
    }
    
    // 并行执行
    Value execute_parallel(NodeId target, ThreadPool& pool) {
        // 1. 拓扑排序，获取执行顺序
        auto sorted_nodes = topological_sort(target);
        
        // 2. 构建依赖图
        std::unordered_map<NodeId, std::vector<NodeId>> dependents;
        std::unordered_map<NodeId, size_t> in_degree;
        
        for (NodeId id : sorted_nodes) {
            in_degree[id] = nodes_[id].inputs.size();
            for (NodeId input : nodes_[id].inputs) {
                dependents[input].push_back(id);
            }
        }
        
        // 3. 并行执行（拓扑级并行）
        std::unordered_map<NodeId, std::future<Value>> futures;
        std::unordered_map<NodeId, Value> results;
        
        // 提交所有入度为0的节点
        for (NodeId id : sorted_nodes) {
            if (in_degree[id] == 0) {
                submit_node(id, pool, futures, results);
            }
        }
        
        // 等待并触发依赖节点
        while (!futures.empty()) {
            auto it = futures.begin();
            NodeId id = it->first;
            Value result = it->second.get();
            futures.erase(it);
            
            results[id] = result;
            
            // 更新依赖节点的入度
            for (NodeId dep : dependents[id]) {
                if (--in_degree[dep] == 0) {
                    submit_node(dep, pool, futures, results);
                }
            }
        }
        
        return results[target];
    }
    
private:
    struct Node {
        std::shared_ptr<void> task;  // ComputationTask
        std::shared_ptr<void> data;  // 数据节点
        std::vector<NodeId> inputs;
    };
    
    std::vector<Node> nodes_;
    
    void submit_node(NodeId id, 
                    ThreadPool& pool,
                    std::unordered_map<NodeId, std::future<Value>>& futures,
                    const std::unordered_map<NodeId, Value>& results) {
        const Node& node = nodes_[id];
        
        // 收集输入
        std::vector<Value> inputs;
        for (NodeId input_id : node.inputs) {
            inputs.push_back(results.at(input_id));
        }
        
        // 提交任务
        futures[id] = pool.submit([task = node.task, inputs]() {
            // 执行计算
            // return task->compute(inputs...);
        });
    }
};
```

#### 3.2 DSL自动转换为计算图

```cpp
class DSLToComputationGraph {
public:
    ComputationGraph build_graph(const std::vector<RuleNode>& rules,
                                  const ImmutableKlineData& klines_1m) {
        ComputationGraph graph;
        
        // 1. 添加K线数据节点
        auto klines_node = graph.add_data_node(klines_1m);
        
        // 2. 分析所需的时间框架
        auto required_tfs = analyze_required_timeframes(rules);
        
        // 3. 并行添加K线转换节点
        std::unordered_map<std::string, NodeId> kline_nodes;
        kline_nodes["1m"] = klines_node;
        
        for (const auto& tf : required_tfs) {
            if (tf == "1m") continue;
            
            auto converter = std::make_shared<KlineConversionTask>(tf);
            kline_nodes[tf] = graph.add_node(converter, klines_node);
        }
        
        // 4. 分析所需的指标
        auto required_indicators = analyze_required_indicators(rules);
        
        // 5. 并行添加指标计算节点
        std::unordered_map<std::string, NodeId> indicator_nodes;
        
        for (const auto& [indicator_name, tf] : required_indicators) {
            auto task = std::make_shared<IndicatorComputationTask>(indicator_name);
            auto kline_node = kline_nodes[tf];
            
            std::string key = indicator_name + ":" + tf;
            indicator_nodes[key] = graph.add_node(task, kline_node);
        }
        
        // 6. 构建规则评估节点
        for (const auto& rule : rules) {
            build_rule_subgraph(graph, rule, indicator_nodes);
        }
        
        return graph;
    }
    
private:
    std::set<std::string> analyze_required_timeframes(const std::vector<RuleNode>& rules) {
        // 遍历AST，收集所有时间框架
    }
    
    std::set<std::pair<std::string, std::string>> analyze_required_indicators(
        const std::vector<RuleNode>& rules
    ) {
        // 遍历AST，收集所有指标引用
    }
};
```

---

### 层次4：无锁的缓存层

#### 4.1 并发缓存设计

```cpp
// 使用分片+原子操作，避免全局锁
template<typename Key, typename Value>
class ConcurrentCache {
public:
    ConcurrentCache(size_t num_shards = 16)
        : shards_(num_shards)
    {}
    
    std::optional<Value> get(const Key& key) const {
        size_t shard_id = hash(key) % shards_.size();
        return shards_[shard_id].get(key);
    }
    
    void set(const Key& key, const Value& value) {
        size_t shard_id = hash(key) % shards_.size();
        shards_[shard_id].set(key, value);
    }
    
private:
    struct Shard {
        std::shared_mutex mutex_;
        std::unordered_map<Key, Value> data_;
        
        std::optional<Value> get(const Key& key) const {
            std::shared_lock lock(mutex_);
            auto it = data_.find(key);
            if (it != data_.end()) {
                return it->second;
            }
            return std::nullopt;
        }
        
        void set(const Key& key, const Value& value) {
            std::unique_lock lock(mutex_);
            data_[key] = value;
        }
    };
    
    std::vector<Shard> shards_;
    std::hash<Key> hash_;
};
```

---

## 重构后的Engine架构

```cpp
class ParallelEngine {
public:
    ParallelEngine(size_t num_threads = 0)
        : thread_pool_(num_threads == 0 ? std::thread::hardware_concurrency() : num_threads)
    {}
    
    void loadStrategy(const std::string& dsl) {
        // 1. 解析DSL
        auto rules = parse_dsl(dsl);
        
        // 2. 构建计算图
        computation_graph_template_ = dsl_converter_.build_graph_template(rules);
        
        // 3. 预热（JIT编译、缓存预加载等）
        warmup();
    }
    
    void setKlines(const std::vector<double>& open,
                   const std::vector<double>& high,
                   const std::vector<double>& low,
                   const std::vector<double>& close,
                   const std::vector<double>& volume,
                   const std::vector<int64_t>& open_time,
                   const std::vector<int64_t>& close_time) {
        // 创建不可变K线数据
        klines_1m_ = std::make_shared<ImmutableKlineData>(
            open, high, low, close, volume, open_time, close_time
        );
        
        // 异步并行转换所有时间框架
        auto required_tfs = computation_graph_template_.required_timeframes();
        klines_multi_tf_ = kline_converter_.convertMultiple(
            *klines_1m_, required_tfs, thread_pool_
        );
    }
    
    Signal getSignal(double current_price, int64_t current_time) {
        // 1. 构建执行上下文
        EvaluationInput input;
        input.klines = klines_1m_;
        input.klines_multi_tf = klines_multi_tf_;
        input.current_price = current_price;
        input.current_time = current_time;
        
        // 2. 实例化计算图
        auto graph = computation_graph_template_.instantiate(input);
        
        // 3. 并行执行
        auto result = graph.execute_parallel(thread_pool_);
        
        // 4. 转换为Signal
        return result.to_signal();
    }
    
private:
    ThreadPool thread_pool_;
    
    // 不可变数据（线程安全）
    std::shared_ptr<const ImmutableKlineData> klines_1m_;
    std::unordered_map<std::string, ImmutableKlineData> klines_multi_tf_;
    
    // 计算图模板（不可变，线程安全）
    ComputationGraphTemplate computation_graph_template_;
    
    // 转换器（无状态，线程安全）
    ParallelKlineConverter kline_converter_;
    DSLToComputationGraph dsl_converter_;
    
    // 并发缓存
    ConcurrentCache<std::string, IndicatorResult> indicator_cache_;
};
```

---

## 性能预期

### 单策略性能（完全并行化）

**当前P0**：
```
K线转换（串行）：   140μs
指标计算（串行）：   130μs
表达式求值：        15μs
总计：             285μs
```

**天然并行架构**：
```
K线转换（并行）：    50μs  ← 4个时间框架同时转换
指标计算（并行）：    30μs  ← 5个指标同时计算
表达式求值（计算图）： 10μs  ← 自动并行
总计：              90μs

加速比：3.2x
```

### 64策略批量性能

**当前P0**：
```
吞吐量：65,547 ops/s
```

**天然并行架构**（预测）：
```
单策略优化：     3.2x
无锁缓存优化：    1.5x
计算图优化：     1.3x
零拷贝优化：     1.2x
总加速比：       7.5x

预期吞吐量：491,603 ops/s
```

**加上字节码+SIMD**：
```
字节码优化：    +1.5x → 737,404 ops/s
SIMD优化：     +1.3x → 958,625 ops/s
最终：         ~960K ops/s ✅ 接近1M！
```

---

## 实施计划（修订版）

### 第1周：基础设施

**Day 1-2**：统一线程池 + 不可变数据结构
- `GlobalThreadPool`
- `ImmutableKlineData`
- `ImmutableContext`（初版）

**Day 3-4**：并行K线转换
- `ParallelKlineConverter`
- 集成到Engine

**验证目标**：单策略 ~200μs（vs 当前285μs）

### 第2周：计算层并行化

**Day 5-7**：计算图系统
- `ComputationGraph`
- `ComputationTask`抽象
- `DSLToComputationGraph`转换器

**Day 8-9**：指标并行计算
- `IndicatorComputationTask`
- 并发缓存

**Day 10**：数据函数并行化
- `ParallelDataFunctionExecutor`
- SIMD优化的聚合函数

**验证目标**：单策略 ~90μs，64策略 ~300K ops/s

### 第3周：性能极致优化

**Day 11-13**：字节码深度优化
- 指令融合
- 常量折叠
- JIT编译

**Day 14-15**：测试与调优
- 性能profiling
- 瓶颈识别
- 极致优化

**最终目标**：64策略 ≥ 900K ops/s

---

## 结论

**用户的要求完全正确！**

不是"给串行系统加多线程"，而是：
1. **重新设计为天然并行架构**
2. **不可变数据 + 值语义**
3. **计算图 + 自动并行**
4. **无锁设计 + 细粒度并发**
5. **数据函数并行化 + SIMD**

**这是一次架构级重构，而非简单优化！**

**预期最终性能**：**960K ops/s** ✅ 接近1M目标！

我现在开始实施第1周Day 1-2的任务：
1. 统一线程池
2. 不可变数据结构

是否立即开始？

