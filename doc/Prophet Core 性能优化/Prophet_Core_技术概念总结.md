# Prophet Core 技术概念总结

**目的**：记录优化过程中涉及的技术概念和名词  
**受众**：学习和回顾  
**日期**：2025-11-01

---

## 📚 目录

1. [P0基准 - 基础概念](#p0基准---基础概念)
2. [P1优化 - 架构模式](#p1优化---架构模式)
3. [P2优化 - 编译技术](#p2优化---编译技术)
4. [P3优化 - 硬件加速](#p3优化---硬件加速)
5. [天然并行 - 并发编程](#天然并行---并发编程)
6. [性能测试 - 测量方法](#性能测试---测量方法)
7. [C++技术 - 语言特性](#c技术---语言特性)
8. [Python绑定 - 跨语言](#python绑定---跨语言)

---

## P0基准 - 基础概念

### 1. DSL (Domain Specific Language)
**领域特定语言**

**定义**：为特定应用领域设计的专用编程语言

**Prophet DSL示例**：
```
ALL{$(5m).RSI().value > 70} = SELL;
```

**组成部分**：
- 信号函数：`ALL`, `ANY`, `NONE`
- 指标引用：`$(5m).RSI`
- 字段访问：`.value`, `.trend`
- 比较运算：`>`, `<`, `==`
- 交易动作：`BUY`, `SELL`, `HOLD`

---

### 2. AST (Abstract Syntax Tree)
**抽象语法树**

**定义**：源代码的树状表示，忽略语法细节

**例子**：
```
DSL: $(5m).RSI().value > 70

AST:
    CompareNode(>)
    ├── IndicatorRefNode
    │   ├── name: "RSI"
    │   ├── timeframe: "5m"
    │   └── field: "value"
    └── NumberNode(70)
```

**用途**：
- 编译器的中间表示
- 便于分析和转换
- 独立于具体语法

---

### 3. Lexer & Parser
**词法分析器 & 语法分析器**

**Lexer（词法分析）**：
```cpp
输入："$(5m).RSI().value > 70"
输出：[
    Token(INDICATOR_REF, "$"),
    Token(DOT, "."),
    Token(IDENTIFIER, "RSI"),
    Token(LPAREN, "("),
    Token(TIMEFRAME, "5m"),
    Token(RPAREN, ")"),
    ...
]
```

**Parser（语法分析）**：
```cpp
输入：Token流
输出：AST（抽象语法树）
```

**关系**：Lexer → Tokens → Parser → AST

---

### 4. Context (上下文)
**执行上下文**

**定义**：存储策略运行时数据的对象

**包含**：
- 指标数据：RSI、MACD等
- 参数值：周期、阈值等
- K线数据：OHLCV
- 环境变量：当前价格、时间

**使用场景**：
```cpp
Context context;
context.setIndicator("RSI", "5m", {{"value", 75.3}});
auto value = context.getIndicator("RSI", "5m", "value");
```

---

## P1优化 - 架构模式

### 1. God Object（上帝对象）
**反模式**

**定义**：一个类承担了太多职责，违反单一职责原则

**问题**：
```cpp
// ❌ God Object
class Context {
    // 指标管理
    std::map<string, IndicatorData> indicators_;
    
    // 参数管理
    std::map<string, ParamData> parameters_;
    
    // K线管理
    std::map<string, KlineData> klines_;
    
    // 缓存管理
    std::map<string, CachedValue> cache_;
    
    // ...还有更多职责
};
```

**影响**：
- 代码难以维护
- 测试困难
- 耦合度高
- 并发不安全

---

### 2. Separation of Concerns（职责分离）
**设计原则**

**定义**：将不同关注点分离到独立的模块

**P1重构**：
```cpp
// ✅ 职责分离
class Context {
    IndicatorCache indicator_cache_;    // 指标缓存
    ParameterStore parameter_store_;    // 参数存储
    KlineManager kline_manager_;        // K线管理
};
```

**好处**：
- 每个类职责单一
- 易于测试
- 易于维护
- 便于并发

---

### 3. Cache Invalidation（缓存失效）
**缓存策略**

**定义**：确定何时清除或更新缓存数据

**计算机科学两大难题之一**：
> "There are only two hard things in Computer Science: cache invalidation and naming things." - Phil Karlton

**P1策略**：
```cpp
class IndicatorCache {
    // 智能缓存失效
    void setKlines(...) {
        kline_version_++;  // K线版本号递增
        cache_.clear();    // 清除所有缓存
    }
    
    auto getIndicator(name, timeframe, field) {
        if (cache_version_ != kline_version_) {
            // 缓存失效，重新计算
            recalculate();
        }
        return cached_value;
    }
};
```

**挑战**：
- 何时失效？（数据变化时）
- 粒度如何？（全部 vs 部分）
- 性能影响？（清除成本 vs 计算成本）

---

### 4. Thread Safety（线程安全）
**并发编程基础**

**定义**：多线程环境下代码行为正确

**P1实现**：
```cpp
class IndicatorCache {
    mutable std::shared_mutex mutex_;  // 读写锁
    
    auto getIndicator(...) const {
        std::shared_lock lock(mutex_);  // 共享锁（读）
        return cache_[key];
    }
    
    void setIndicator(...) {
        std::unique_lock lock(mutex_);  // 独占锁（写）
        cache_[key] = value;
    }
};
```

**锁类型**：
- `std::mutex`：互斥锁（独占）
- `std::shared_mutex`：读写锁（读共享，写独占）
- `std::lock_guard`：RAII锁（自动释放）
- `std::unique_lock`：独占锁
- `std::shared_lock`：共享锁

---

## P2优化 - 编译技术

### 1. Bytecode（字节码）
**中间表示**

**定义**：介于源代码和机器码之间的指令集

**为什么需要字节码？**
- AST遍历开销大（递归调用）
- 字节码顺序执行（无递归）
- 指令级优化空间

**Prophet字节码示例**：
```cpp
// DSL: $(5m).RSI().value > 70

字节码：
0: LOAD_INDICATOR  "RSI" "5m" "value"  // 加载指标
1: PUSH_CONST      70                  // 压入常量
2: COMPARE_GT                          // 大于比较
3: RETURN                              // 返回结果
```

**对比**：
```
AST遍历：      CompareNode → evaluate()
               ├─> IndicatorNode → evaluate()
               └─> NumberNode → evaluate()
               
字节码执行：    顺序执行指令0→1→2→3
```

---

### 2. Virtual Machine（虚拟机）
**字节码执行引擎**

**定义**：解释执行字节码的软件

**Prophet BytecodeVM**：
```cpp
class BytecodeVM {
    std::vector<Value> stack_;  // 操作数栈
    
    Value execute(const vector<Instruction>& code) {
        for (auto& instr : code) {
            switch (instr.opcode) {
                case PUSH_CONST:
                    stack_.push_back(instr.operand);
                    break;
                    
                case ADD:
                    auto b = stack_.pop();
                    auto a = stack_.pop();
                    stack_.push_back(a + b);
                    break;
                    
                // ... 其他指令
            }
        }
        return stack_.back();
    }
};
```

**经典虚拟机**：
- JVM（Java Virtual Machine）
- Python解释器
- .NET CLR

---

### 3. JIT (Just-In-Time Compilation)
**即时编译**

**定义**：运行时将字节码编译为机器码

**优势**：
- 比解释执行快（直接机器码）
- 比AOT灵活（可运行时优化）

**Prophet简化JIT**：
```cpp
class JITCompiler {
    auto compile(const Bytecode& code) {
        // 生成Lambda函数
        return [captured_code](Context& ctx) {
            // 直接C++代码，无解释开销
            auto value = ctx.getIndicator("RSI", "5m", "value");
            return value > 70;
        };
    }
};
```

**注意**：Prophet的是"简化JIT"，真正的JIT会生成机器码

**真正的JIT**（如V8、LuaJIT）：
- 生成x86/ARM机器码
- CPU寄存器分配
- 指令重排序

---

### 4. Instruction Set（指令集）
**虚拟机指令**

**Prophet指令集**：
```cpp
enum class Opcode {
    // 栈操作
    PUSH_CONST,          // 压入常量
    POP,                 // 弹出栈顶
    
    // 数据加载
    LOAD_INDICATOR,      // 加载指标
    LOAD_PARAM,          // 加载参数
    LOAD_ENV,            // 加载环境变量
    
    // 算术运算
    ADD, SUB, MUL, DIV, MOD,
    
    // 比较运算
    EQ, NE, LT, LE, GT, GE,
    
    // 逻辑运算
    AND, OR, NOT,
    
    // 控制流
    JUMP, JUMP_IF_FALSE, RETURN
};
```

**类比**：CPU指令集（x86、ARM）

---

### 5. Stack Machine（栈机器）
**计算模型**

**定义**：使用栈进行计算的虚拟机

**示例**：计算 `(2 + 3) * 4`
```
指令序列：
1. PUSH 2      栈：[2]
2. PUSH 3      栈：[2, 3]
3. ADD         栈：[5]        (2+3=5)
4. PUSH 4      栈：[5, 4]
5. MUL         栈：[20]       (5*4=20)
```

**对比**：寄存器机器（Register Machine）
```
指令序列：
1. MOV R1, 2
2. MOV R2, 3
3. ADD R1, R2    (R1 = 2+3)
4. MOV R2, 4
5. MUL R1, R2    (R1 = 5*4)
```

**优势**：
- 栈机器：指令简单，易实现
- 寄存器机器：更快，更接近硬件

---

## P3优化 - 硬件加速

### 1. SIMD (Single Instruction, Multiple Data)
**单指令多数据流**

**定义**：一条指令同时处理多个数据

**图示**：
```
标量计算（普通）：
a[0] + b[0] → c[0]
a[1] + b[1] → c[1]
a[2] + b[2] → c[2]
a[3] + b[3] → c[3]
共4条指令

SIMD计算：
[a[0], a[1], a[2], a[3]] + [b[0], b[1], b[2], b[3]] → [c[0], c[1], c[2], c[3]]
共1条指令（4路并行）
```

**代码对比**：
```cpp
// 标量
for (int i = 0; i < 1000; ++i) {
    c[i] = a[i] + b[i];  // 1000次循环
}

// SIMD (AVX, 4-way)
for (int i = 0; i < 1000; i += 4) {
    __m256d va = _mm256_loadu_pd(&a[i]);
    __m256d vb = _mm256_loadu_pd(&b[i]);
    __m256d vc = _mm256_add_pd(va, vb);
    _mm256_storeu_pd(&c[i], vc);
}
// 250次循环（4倍速）
```

---

### 2. CPU指令集扩展
**SIMD演进**

| 指令集 | 年份 | 位宽 | 并行度（double） |
|--------|------|------|------------------|
| SSE | 1999 | 128-bit | 2路 |
| SSE2 | 2001 | 128-bit | 2路 |
| AVX | 2011 | 256-bit | 4路 |
| AVX2 | 2013 | 256-bit | 4路 |
| AVX-512 | 2016 | 512-bit | 8路 |

**检测CPU支持**：
```cpp
bool has_avx2() {
    int cpu_info[4];
    __cpuid(cpu_info, 0);
    if (cpu_info[0] >= 7) {
        __cpuidex(cpu_info, 7, 0);
        return (cpu_info[1] & (1 << 5)) != 0;  // Check AVX2 bit
    }
    return false;
}
```

---

### 3. Intrinsics（内建函数）
**SIMD编程接口**

**定义**：编译器提供的直接访问CPU指令的函数

**AVX内建函数**：
```cpp
// 数据类型
__m256d      // 4个double（256位）
__m256       // 8个float（256位）
__m256i      // 整数向量

// 常用操作
_mm256_loadu_pd(ptr)              // 加载（unaligned）
_mm256_storeu_pd(ptr, vec)        // 存储
_mm256_add_pd(a, b)               // 加法
_mm256_sub_pd(a, b)               // 减法
_mm256_mul_pd(a, b)               // 乘法
_mm256_div_pd(a, b)               // 除法
_mm256_cmp_pd(a, b, CMP_GT_OQ)   // 比较
_mm256_set1_pd(value)             // 广播（所有元素相同）
```

**为什么不用汇编？**
- 内建函数更易读
- 编译器可优化
- 可移植性更好

---

### 4. Vectorization（向量化）
**自动/手动向量化**

**自动向量化**（编译器）：
```cpp
// 代码
for (int i = 0; i < 1000; ++i) {
    c[i] = a[i] + b[i];
}

// 编译器自动生成SIMD代码（需要-O3 -mavx2）
```

**手动向量化**（P3做法）：
```cpp
void add_simd(const double* a, const double* b, double* c, size_t n) {
    size_t i = 0;
    
    // SIMD处理（4路并行）
    for (; i + 4 <= n; i += 4) {
        __m256d va = _mm256_loadu_pd(&a[i]);
        __m256d vb = _mm256_loadu_pd(&b[i]);
        __m256d vc = _mm256_add_pd(va, vb);
        _mm256_storeu_pd(&c[i], vc);
    }
    
    // 处理剩余元素
    for (; i < n; ++i) {
        c[i] = a[i] + b[i];
    }
}
```

**为什么手动？**
- 编译器可能不够聪明
- 特定算法需要优化
- 需要跨平台控制

---

### 5. Alignment（内存对齐）
**性能关键**

**定义**：数据地址是某个值的倍数

**为什么重要？**
- SIMD加载对齐数据更快
- 未对齐访问可能很慢（甚至崩溃）

**代码示例**：
```cpp
// ❌ 未对齐（可能慢）
double* data = new double[100];
__m256d vec = _mm256_loadu_pd(data);  // 'u' = unaligned

// ✅ 对齐到32字节（AVX要求）
alignas(32) double data[100];
__m256d vec = _mm256_load_pd(data);   // 更快
```

**C++11对齐控制**：
```cpp
alignas(32) double array[100];        // 数组对齐
struct alignas(16) Vec4 { ... };      // 结构体对齐
auto ptr = std::aligned_alloc(32, size);  // 动态分配对齐内存
```

---

## 天然并行 - 并发编程

### 1. Thread Pool（线程池）
**资源管理模式**

**为什么需要？**
- 创建线程成本高（~1ms）
- 线程数过多导致调度开销
- 资源管理困难

**实现**：
```cpp
class ThreadPool {
    std::vector<std::thread> workers_;           // 工作线程
    std::queue<std::function<void()>> tasks_;    // 任务队列
    std::mutex queue_mutex_;                     // 队列锁
    std::condition_variable condition_;          // 条件变量
    
public:
    ThreadPool(size_t threads) {
        for (size_t i = 0; i < threads; ++i) {
            workers_.emplace_back([this] {
                // 工作线程循环
                while (true) {
                    std::function<void()> task;
                    {
                        std::unique_lock lock(queue_mutex_);
                        condition_.wait(lock, [this] {
                            return !tasks_.empty() || stop_;
                        });
                        
                        if (stop_) return;
                        
                        task = std::move(tasks_.front());
                        tasks_.pop();
                    }
                    task();  // 执行任务
                }
            });
        }
    }
};
```

**使用**：
```cpp
ThreadPool pool(8);  // 8个工作线程
auto future = pool.submit([]{ return calculate(); });
auto result = future.get();
```

---

### 2. Future & Promise
**异步编程**

**定义**：
- `Promise`：承诺将来提供一个值
- `Future`：未来可以获取的值

**使用场景**：
```cpp
// 异步计算
std::promise<int> promise;
std::future<int> future = promise.get_future();

// 在另一个线程中设置值
std::thread([&promise] {
    int result = expensive_calculation();
    promise.set_value(result);
}).detach();

// 主线程等待结果
int result = future.get();  // 阻塞直到结果可用
```

**优势**：
- 类型安全
- 异常传播
- 自动同步

---

### 3. Lock-Free（无锁编程）
**高级并发技术**

**定义**：不使用锁实现并发安全

**技术**：
- **原子操作**：`std::atomic<T>`
- **CAS**：Compare-And-Swap
- **Memory Order**：内存序

**示例**：
```cpp
// 无锁计数器
class LockFreeCounter {
    std::atomic<int> count_{0};
    
public:
    void increment() {
        count_.fetch_add(1, std::memory_order_relaxed);
    }
    
    int get() const {
        return count_.load(std::memory_order_acquire);
    }
};
```

**优势**：
- 无锁竞争
- 可扩展性好

**劣势**：
- 难以实现
- 难以调试
- 只适用特定场景

---

### 4. Immutability（不可变性）
**函数式编程概念**

**定义**：对象创建后不可修改

**为什么重要？**
- 天然线程安全（无竞态条件）
- 易于推理
- 可安全共享

**Prophet实现**：
```cpp
class ImmutableKlineData {
    const std::vector<double> close_;  // const成员
    
public:
    ImmutableKlineData(std::vector<double> close)
        : close_(std::move(close)) {}
    
    // 只有const方法
    const double* data() const { return close_.data(); }
    size_t size() const { return close_.size(); }
    
    // ❌ 无修改方法
};
```

**多线程使用**：
```cpp
// 创建不可变数据
auto klines = std::make_shared<ImmutableKlineData>(...);

// 多线程安全共享
thread1: auto data = klines->data();  // ✅ 安全
thread2: auto data = klines->data();  // ✅ 安全
```

---

### 5. Data Race（数据竞争）
**并发Bug**

**定义**：多线程同时访问同一内存，至少一个是写操作

**示例**：
```cpp
int counter = 0;

// ❌ 数据竞争
thread1: counter++;  // 读-改-写
thread2: counter++;  // 读-改-写

// 可能结果：1（应该是2）
```

**为什么危险？**
```cpp
counter++  实际上是三步：
1. 读取 counter (假设值为0)
2. 加1 (计算得1)
3. 写回 counter

并发执行：
Thread1: 读(0) → 加1 → 写(1)
Thread2:    读(0) → 加1 → 写(1)
结果：counter = 1（丢失一次更新）
```

**解决方案**：
```cpp
// 方案1：锁
std::mutex mtx;
thread1: { std::lock_guard lock(mtx); counter++; }
thread2: { std::lock_guard lock(mtx); counter++; }

// 方案2：原子操作
std::atomic<int> counter{0};
thread1: counter++;
thread2: counter++;
```

---

### 6. Amdahl's Law（阿姆达尔定律）
**并行加速上限**

**公式**：
```
加速比 = 1 / (串行比例 + 并行比例/线程数)

例如：
- 串行部分：40%
- 并行部分：60%
- 线程数：8

加速比 = 1 / (0.4 + 0.6/8)
       = 1 / (0.4 + 0.075)
       = 1 / 0.475
       = 2.1倍

即使有8个线程，只能加速2.1倍！
```

**关键洞察**：
- 串行部分限制了并行加速
- 必须优化串行部分

**Prophet遇到的问题**：
- Python循环SetKlines（串行）：57%
- 并行evaluate（并行）：43%
- 理论最大加速：1.7x
- 实际加速：1.96x（接近理论极限）

---

### 7. False Sharing（伪共享）
**缓存行竞争**

**定义**：不同线程访问同一缓存行的不同数据

**CPU缓存行**：
- 通常64字节
- CPU以缓存行为单位加载数据

**问题**：
```cpp
struct {
    int counter1;  // Thread 1 使用
    int counter2;  // Thread 2 使用
} data;  // 两个int在同一缓存行

// 虽然没有数据竞争，但有伪共享
Thread 1: data.counter1++;  // 使缓存行失效
Thread 2: data.counter2++;  // 缓存行失效，需重新加载
```

**解决方案**：
```cpp
struct alignas(64) {  // 强制对齐到缓存行
    int counter1;
    char padding[60];  // 填充到64字节
} data1;

struct alignas(64) {
    int counter2;
    char padding[60];
} data2;

// 现在在不同缓存行，无伪共享
```

---

## 性能测试 - 测量方法

### 1. Benchmarking（基准测试）
**性能测量**

**基本方法**：
```cpp
auto start = std::chrono::high_resolution_clock::now();

// 执行被测代码
for (int i = 0; i < 1000; ++i) {
    function_to_test();
}

auto end = std::chrono::high_resolution_clock::now();
auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);

std::cout << "耗时: " << duration.count() / 1000.0 << "μs/次\n";
```

**注意事项**：
1. **预热**：首次运行可能更慢（缓存冷启动）
2. **重复测试**：多次运行取平均/中位数
3. **异常值处理**：去除异常值（3σ法则）
4. **环境稳定**：关闭后台程序、固定CPU频率

---

### 2. Profiling（性能分析）
**找到性能瓶颈**

**工具**：
- **Visual Studio Profiler**（Windows）
- **Valgrind/Callgrind**（Linux）
- **perf**（Linux）
- **Intel VTune**
- **手动计时**

**分析指标**：
- **Hot Spot**：耗时最多的函数
- **调用次数**：函数被调用多少次
- **调用树**：函数调用关系

**Prophet使用**：
```cpp
// 手动计时
struct Timer {
    std::chrono::time_point<...> start;
    const char* name;
    
    Timer(const char* n) : name(n) {
        start = std::chrono::high_resolution_clock::now();
    }
    
    ~Timer() {
        auto end = std::chrono::high_resolution_clock::now();
        auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
        std::cout << name << ": " << duration.count() << "μs\n";
    }
};

void function() {
    Timer t("function");
    // ... 代码 ...
}  // 自动输出耗时
```

---

### 3. Statistical Significance（统计显著性）
**结果可靠性**

**为什么重要？**
- 性能测试有噪声
- 小差异可能是随机波动

**指标**：

**1. 中位数 vs 平均值**
```
数据：[10, 11, 12, 13, 100]
平均值：29.2μs（受异常值影响）
中位数：12μs（更稳定）

推荐：使用中位数
```

**2. 标准差（Standard Deviation）**
```
数据：[10, 11, 12, 13, 14]
平均值：12μs
标准差：1.58μs

解释：结果波动±1.58μs
```

**3. 变异系数（Coefficient of Variation）**
```
CV = (标准差 / 平均值) × 100%

CV < 5%：非常稳定 ✅
CV < 10%：稳定 ✅
CV < 15%：一般 →
CV ≥ 15%：不稳定 ⚠️
```

**4. 3σ法则（异常值过滤）**
```cpp
// 过滤异常值
mean = np.mean(times)
std = np.std(times)
mask = np.abs(times - mean) < 3 * std
filtered = times[mask]
```

---

### 4. Warm-up（预热）
**测试前准备**

**为什么需要？**
- **缓存冷启动**：首次访问数据慢
- **JIT编译**：首次执行可能触发编译
- **CPU频率调整**：CPU可能动态调频

**实践**：
```python
# ❌ 不预热
times = []
for i in range(100):
    start = time.time()
    result = function()
    times.append(time.time() - start)

# ✅ 预热
for i in range(50):  # 预热50次
    function()

times = []
for i in range(100):  # 正式测试
    start = time.time()
    result = function()
    times.append(time.time() - start)
```

---

## C++技术 - 语言特性

### 1. RAII (Resource Acquisition Is Initialization)
**资源管理习惯**

**定义**：资源的生命周期与对象绑定

**示例**：
```cpp
// ❌ 手动管理（易出错）
void function() {
    std::mutex* mtx = new std::mutex();
    mtx->lock();
    
    // ... 代码可能抛异常 ...
    
    mtx->unlock();  // 可能不会执行！
    delete mtx;
}

// ✅ RAII（自动管理）
void function() {
    std::mutex mtx;
    std::lock_guard<std::mutex> lock(mtx);
    
    // ... 代码即使抛异常 ...
    
    // lock析构时自动unlock
}
```

**Prophet中的RAII**：
- `std::unique_ptr`：自动释放内存
- `std::lock_guard`：自动释放锁
- `std::shared_ptr`：引用计数

---

### 2. Move Semantics（移动语义）
**C++11核心特性**

**问题**：
```cpp
std::vector<int> create_large_vector() {
    std::vector<int> v(1000000);
    return v;  // 拷贝？
}

auto v = create_large_vector();  // 昂贵的拷贝？
```

**解决**：
```cpp
// C++11移动
std::vector<int> v = create_large_vector();
// ✅ 编译器自动使用移动构造函数
// 只移动指针，不拷贝数据

// 显式移动
std::vector<int> v1(1000000);
std::vector<int> v2 = std::move(v1);  // v1内容移动到v2
// v1现在为空
```

**Prophet使用**：
```cpp
class BatchEngine {
    std::unique_ptr<ThreadPool> pool_;
    
    BatchEngine(BatchEngine&& other) = default;  // 允许移动
    BatchEngine(const BatchEngine&) = delete;    // 禁止拷贝
};
```

---

### 3. Smart Pointers（智能指针）
**自动内存管理**

**类型**：

**`std::unique_ptr`**：独占所有权
```cpp
auto ptr = std::make_unique<Engine>();
// ptr析构时自动删除Engine

// ❌ 不能拷贝
auto ptr2 = ptr;  // 编译错误

// ✅ 可以移动
auto ptr2 = std::move(ptr);  // ptr变为nullptr
```

**`std::shared_ptr`**：共享所有权
```cpp
auto ptr1 = std::make_shared<Data>();
auto ptr2 = ptr1;  // ✅ 可以拷贝，引用计数+1

// 两个ptr析构后，Data才被删除
```

**`std::weak_ptr`**：弱引用（不增加引用计数）
```cpp
std::shared_ptr<Node> node = std::make_shared<Node>();
std::weak_ptr<Node> weak = node;  // 弱引用

// 使用前需要lock
if (auto ptr = weak.lock()) {
    // ptr是shared_ptr，临时增加引用计数
    ptr->do_something();
}
```

---

### 4. Template（模板）
**泛型编程**

**函数模板**：
```cpp
template<typename T>
T max(T a, T b) {
    return a > b ? a : b;
}

max(3, 5);      // T = int
max(3.5, 2.1);  // T = double
```

**类模板**：
```cpp
template<typename T>
class Stack {
    std::vector<T> data_;
    
public:
    void push(T value) { data_.push_back(value); }
    T pop() { ... }
};

Stack<int> int_stack;
Stack<std::string> str_stack;
```

**Prophet使用**：
```cpp
template<class F, class... Args>
auto ThreadPool::submit(F&& f, Args&&... args)
    -> std::future<typename std::result_of<F(Args...)>::type>
{
    // ...
}

// 可以提交任何可调用对象
pool.submit([]{ return 42; });
pool.submit([](int x){ return x * 2; }, 21);
```

---

### 5. Lambda（Lambda表达式）
**匿名函数**

**语法**：
```cpp
[capture](parameters) -> return_type { body }
```

**捕获方式**：
```cpp
int x = 10;

[x]()      // 按值捕获x（拷贝）
[&x]()     // 按引用捕获x
[=]()      // 按值捕获所有外部变量
[&]()      // 按引用捕获所有
[this]()   // 捕获当前对象指针
[x, &y]()  // 混合捕获
```

**Prophet使用**：
```cpp
// 异步任务
auto future = pool.submit([&engines, price, time, i]() -> Signal {
    return engines[i]->getSignal(price, time);
});

// 等价于定义函数：
Signal task(decltype(engines)& engines, double price, int64_t time, size_t i) {
    return engines[i]->getSignal(price, time);
}
```

---

## Python绑定 - 跨语言

### 1. pybind11
**C++/Python绑定库**

**定义**：让Python调用C++代码

**基本用法**：
```cpp
#include <pybind11/pybind11.h>

namespace py = pybind11;

PYBIND11_MODULE(Core, m) {
    // 绑定函数
    m.def("add", [](int a, int b) { return a + b; });
    
    // 绑定类
    py::class_<Engine>(m, "Engine")
        .def(py::init<>())  // 构造函数
        .def("load_strategy", &Engine::load_strategy)
        .def("get_signal", &Engine::getSignal);
}
```

**Python使用**：
```python
from Prophet import Core

result = Core.add(1, 2)  # 调用C++函数

engine = Core.Engine()
engine.load_strategy("...")
signal = engine.get_signal(50000, 12345)
```

---

### 2. Zero-Copy（零拷贝）
**性能关键**

**问题**：
```python
# ❌ 拷贝数据（慢）
data = np.array([1, 2, 3, ...], dtype=np.float64)  # Python
cpp_function(data)  # 拷贝到C++

# 对于大数组（1000个K线），拷贝成本高
```

**解决**：
```cpp
// ✅ 零拷贝（直接访问NumPy内存）
void setKlines(py::array_t<double> close) {
    auto buf = close.request();
    double* ptr = static_cast<double*>(buf.ptr);  // 直接指针
    size_t size = buf.size;
    
    // 直接使用ptr，无拷贝
}
```

**Python使用**：
```python
close = np.array([...])  # NumPy数组
engine.SetKlines(close)   # 无拷贝，直接传指针
```

---

### 3. GIL (Global Interpreter Lock)
**Python全局解释器锁**

**定义**：Python解释器一次只能执行一个线程

**影响**：
```python
# ❌ Python多线程无法真正并行
import threading

def work():
    for i in range(1000000):
        x = i * i

thread1 = threading.Thread(target=work)
thread2 = threading.Thread(target=work)

# 由于GIL，两个线程串行执行！
```

**解决方案**：
```python
# ✅ C++端多线程（释放GIL）
py::gil_scoped_release release;  // 释放GIL

// C++多线程代码
std::vector<std::thread> threads;
for (...) {
    threads.emplace_back([]{ ... });
}

// Python端可以继续执行
```

---

## 总结：关键概念清单

### 编译原理
- ✅ Lexer（词法分析）
- ✅ Parser（语法分析）
- ✅ AST（抽象语法树）
- ✅ Bytecode（字节码）
- ✅ Virtual Machine（虚拟机）
- ✅ JIT（即时编译）

### 并发编程
- ✅ Thread Pool（线程池）
- ✅ Mutex（互斥锁）
- ✅ Shared Mutex（读写锁）
- ✅ Lock-Free（无锁编程）
- ✅ Atomic（原子操作）
- ✅ Future/Promise（异步编程）
- ✅ Data Race（数据竞争）
- ✅ False Sharing（伪共享）

### 硬件优化
- ✅ SIMD（单指令多数据）
- ✅ AVX/SSE（指令集）
- ✅ Intrinsics（内建函数）
- ✅ Vectorization（向量化）
- ✅ Alignment（内存对齐）
- ✅ Cache Line（缓存行）

### 设计模式
- ✅ RAII（资源管理）
- ✅ God Object（反模式）
- ✅ Separation of Concerns（职责分离）
- ✅ Immutability（不可变性）

### C++特性
- ✅ Move Semantics（移动语义）
- ✅ Smart Pointers（智能指针）
- ✅ Template（模板）
- ✅ Lambda（Lambda表达式）

### Python互操作
- ✅ pybind11（绑定库）
- ✅ Zero-Copy（零拷贝）
- ✅ GIL（全局解释器锁）

### 性能分析
- ✅ Benchmarking（基准测试）
- ✅ Profiling（性能分析）
- ✅ Statistical Significance（统计显著性）
- ✅ Amdahl's Law（阿姆达尔定律）

---

**文档完成日期**: 2025-11-01  
**适合**：学习、回顾、面试准备  
**下一步**：查看技术债分析

