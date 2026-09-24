/*
 * ============================================================================
 * 文件名：benchmarks.cpp
 * 功能说明：Prophet.Core 性能基准测试套件
 * 
 * 包含微观和宏观基准测试
 * ============================================================================
 */

#include "prophet/utils/benchmark.hpp"
#include "prophet/utils/performance_metrics.hpp"
#include "prophet/indicators/calculator.hpp"
#include "prophet/cache/indicator_cache.hpp"
#include "prophet/simd/simd_math.hpp"
#include "prophet/bytecode/compiler.hpp"
#include "prophet/bytecode/vm.hpp"
#include "prophet/core/engine.hpp"
#include <iostream>
#include <random>
#include <cmath>

using namespace prophet::benchmark;
using namespace prophet;

// ============================================================================
// 辅助函数
// ============================================================================

// 生成随机K线数据
std::vector<double> generateRandomPrices(size_t count, double base = 100.0) {
    std::vector<double> prices;
    prices.reserve(count);
    
    std::mt19937 rng(42);  // 固定种子以保证可重复性
    std::normal_distribution<double> dist(base, base * 0.02);
    
    for (size_t i = 0; i < count; ++i) {
        prices.push_back(std::max(1.0, dist(rng)));
    }
    
    return prices;
}

// ============================================================================
// 微观基准测试
// ============================================================================

namespace micro {

/**
 * 基准测试1：指标计算性能
 */
void benchIndicatorCalculation() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               微观基准测试 - 指标计算性能                               ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    BenchmarkSuite suite;
    indicators::Calculator calc;
    
    // 生成测试数据
    auto prices = generateRandomPrices(1000);
    auto high = generateRandomPrices(1000, 102.0);
    auto low = generateRandomPrices(1000, 98.0);
    auto volume = generateRandomPrices(1000, 1000000.0);
    
    // MACD基准
    suite.add("MACD计算 (1000根K线)", [&]() {
        calc.MACD(prices, 12, 26, 9);
    }, 1000);
    
    // RSI基准
    suite.add("RSI计算 (1000根K线)", [&]() {
        calc.RSI(prices, 14);
    }, 1000);
    
    // EMA基准
    suite.add("EMA计算 (1000根K线)", [&]() {
        calc.EMA(prices, 20);
    }, 1000);
    
    // SMA基准
    suite.add("SMA计算 (1000根K线)", [&]() {
        calc.MA(prices, 20);
    }, 1000);
    
    // ATR基准
    suite.add("ATR计算 (1000根K线)", [&]() {
        calc.ATR(high, low, prices, 14);
    }, 1000);
    
    auto results = suite.runAll();
    suite.generateReport(results);
}

/**
 * 基准测试2：缓存性能
 */
void benchCachePerformance() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               微观基准测试 - 缓存性能                                   ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    cache::IndicatorCache cache;
    
    // 填充缓存
    IndicatorResult dummy_result;
    dummy_result["value"] = Value::fromNumber(100.0);
    
    for (int i = 0; i < 100; ++i) {
        cache.set("5m", "MACD_" + std::to_string(i), dummy_result, 1);
    }
    
    // 缓存命中基准
    auto bench_hit = BenchmarkRunner::run("缓存命中查询", [&]() {
        IndicatorResult result;
        cache.get("5m", "MACD_0", 1, result);
    }, 10000);
    
    // 缓存未命中基准
    auto bench_miss = BenchmarkRunner::run("缓存未命中查询", [&]() {
        IndicatorResult result;
        cache.get("5m", "NONEXISTENT", 1, result);
    }, 10000);
    
    // 缓存写入基准
    auto bench_set = BenchmarkRunner::run("缓存写入", [&]() {
        cache.set("5m", "TEST", dummy_result, 1);
    }, 10000);
    
    bench_hit.print();
    bench_miss.print();
    bench_set.print();
}

/**
 * 基准测试3：SIMD vs 标量
 */
void benchSIMDPerformance() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               微观基准测试 - SIMD vs 标量                               ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    const size_t N = 10000;
    std::vector<double> a = generateRandomPrices(N);
    std::vector<double> b = generateRandomPrices(N);
    std::vector<double> result(N);
    
    // SIMD加法
    auto bench_simd_add = BenchmarkRunner::run("SIMD向量加法 (10K元素)", [&]() {
        simd::add(a.data(), b.data(), result.data(), N);
    }, 5000);
    
    // 标量加法（禁用SIMD）
    auto bench_scalar_add = BenchmarkRunner::run("标量向量加法 (10K元素)", [&]() {
        for (size_t i = 0; i < N; ++i) {
            result[i] = a[i] + b[i];
        }
    }, 5000);
    
    bench_simd_add.print();
    bench_scalar_add.print();
    
    // 计算加速比
    double speedup = bench_scalar_add.avg_time_ms / bench_simd_add.avg_time_ms;
    std::cout << "SIMD加速比: " << std::fixed << std::setprecision(2) 
              << speedup << "x\n\n";
}

/**
 * 基准测试4：字节码执行
 */
void benchBytecodeExecution() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               微观基准测试 - 字节码执行                                 ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    // 创建简单表达式：(a > b) AND (c < d)
    using namespace prophet::dsl;
    
    auto a = std::make_shared<NumberNode>(100.0);
    auto b = std::make_shared<NumberNode>(50.0);
    auto c = std::make_shared<NumberNode>(30.0);
    auto d = std::make_shared<NumberNode>(80.0);
    
    auto cmp1 = std::make_shared<BinaryOpNode>(a, b, BinaryOpNode::OpType::GT);
    auto cmp2 = std::make_shared<BinaryOpNode>(c, d, BinaryOpNode::OpType::LT);
    auto expr = std::make_shared<BinaryOpNode>(cmp1, cmp2, BinaryOpNode::OpType::AND);
    
    // 编译为字节码
    bytecode::BytecodeCompiler compiler;
    auto code = compiler.compile(expr);
    
    // VM执行基准
    bytecode::BytecodeVM vm;
    Context context;
    
    auto bench_vm = BenchmarkRunner::run("字节码VM执行", [&]() {
        vm.execute(code, context);
    }, 100000);
    
    // AST执行基准（回退）
    auto bench_ast = BenchmarkRunner::run("AST直接执行", [&]() {
        Evaluator evaluator;
        evaluator.evaluate(expr, context);
    }, 100000);
    
    bench_vm.print();
    bench_ast.print();
    
    double speedup = bench_ast.avg_time_ms / bench_vm.avg_time_ms;
    std::cout << "字节码加速比: " << std::fixed << std::setprecision(2) 
              << speedup << "x\n\n";
}

} // namespace micro

// ============================================================================
// 宏观基准测试
// ============================================================================

namespace macro {

/**
 * 基准测试5：简单策略
 */
void benchSimpleStrategy() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               宏观基准测试 - 简单策略                                   ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    // 简单策略：单一规则，2个时间框架
    std::string simple_dsl = R"(
        ALL{
            $(5m).RSI().value < 30,
            $(15m).EMA().value > $(15m).CLOSE()
        } = BUY;
    )";
    
    strategy::Engine engine(simple_dsl, {});
    
    // 生成K线数据
    auto prices = generateRandomPrices(500);
    std::vector<double> open = prices;
    std::vector<double> high = generateRandomPrices(500, 102.0);
    std::vector<double> low = generateRandomPrices(500, 98.0);
    std::vector<double> close = prices;
    std::vector<double> volume = generateRandomPrices(500, 1000000.0);
    std::vector<int64_t> open_time(500);
    std::vector<int64_t> close_time(500);
    
    for (size_t i = 0; i < 500; ++i) {
        open_time[i] = i * 60000;
        close_time[i] = (i + 1) * 60000;
    }
    
    auto bench = BenchmarkRunner::run("简单策略执行 (1规则, 2时间框架)", [&]() {
        engine.set_klines(open.data(), high.data(), low.data(), close.data(),
                         volume.data(), open_time.data(), close_time.data(), 500);
        engine.get_signal(prices.back(), close_time.back());
    }, 1000);
    
    bench.print();
}

/**
 * 基准测试6：复杂策略
 */
void benchComplexStrategy() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               宏观基准测试 - 复杂策略                                   ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    // 复杂策略：多规则，5个时间框架
    std::string complex_dsl = R"(
        ALL{
            $(5m).RSI().value < 30,
            $(15m).MACD().trend = BULLISH,
            $(1h).EMA().value > $(1h).CLOSE(),
            $(4h).ADX().value > 25
        } = BUY;
        
        ALL{
            $(5m).RSI().value > 70,
            $(15m).MACD().trend = BEARISH,
            $(1h).EMA().value < $(1h).CLOSE(),
            $(4h).ADX().value > 25
        } = SELL;
    )";
    
    strategy::Engine engine(complex_dsl, {});
    
    // 生成更多K线数据（复杂策略需要更多历史）
    auto prices = generateRandomPrices(1000);
    std::vector<double> open = prices;
    std::vector<double> high = generateRandomPrices(1000, 102.0);
    std::vector<double> low = generateRandomPrices(1000, 98.0);
    std::vector<double> close = prices;
    std::vector<double> volume = generateRandomPrices(1000, 1000000.0);
    std::vector<int64_t> open_time(1000);
    std::vector<int64_t> close_time(1000);
    
    for (size_t i = 0; i < 1000; ++i) {
        open_time[i] = i * 60000;
        close_time[i] = (i + 1) * 60000;
    }
    
    auto bench = BenchmarkRunner::run("复杂策略执行 (2规则, 5时间框架)", [&]() {
        engine.set_klines(open.data(), high.data(), low.data(), close.data(),
                         volume.data(), open_time.data(), close_time.data(), 1000);
        engine.get_signal(prices.back(), close_time.back());
    }, 100);
    
    bench.print();
}

/**
 * 基准测试7：多时间框架性能
 */
void benchMultiTimeframe() {
    std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║               宏观基准测试 - 多时间框架                                 ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    BenchmarkSuite suite;
    
    // 测试不同数量的时间框架
    for (int num_timeframes : {2, 3, 5, 7}) {
        std::string dsl = "ALL{";
        
        std::vector<std::string> timeframes = {"5m", "15m", "1h", "4h", "1d", "1w", "1M"};
        for (int i = 0; i < num_timeframes && i < timeframes.size(); ++i) {
            if (i > 0) dsl += ",";
            dsl += "$(" + timeframes[i] + ").RSI().value < 70";
        }
        
        dsl += "} = BUY;";
        
        strategy::Engine engine(dsl, {});
        
        auto prices = generateRandomPrices(1000);
        std::vector<double> open = prices;
        std::vector<double> high = generateRandomPrices(1000, 102.0);
        std::vector<double> low = generateRandomPrices(1000, 98.0);
        std::vector<double> close = prices;
        std::vector<double> volume = generateRandomPrices(1000, 1000000.0);
        std::vector<int64_t> open_time(1000);
        std::vector<int64_t> close_time(1000);
        
        for (size_t i = 0; i < 1000; ++i) {
            open_time[i] = i * 60000;
            close_time[i] = (i + 1) * 60000;
        }
        
        suite.add(std::to_string(num_timeframes) + "个时间框架", [&]() {
            engine.set_klines(open.data(), high.data(), low.data(), close.data(),
                             volume.data(), open_time.data(), close_time.data(), 1000);
            engine.get_signal(prices.back(), close_time.back());
        }, 100);
    }
    
    auto results = suite.runAll();
    suite.generateReport(results);
}

} // namespace macro

// ============================================================================
// 主函数
// ============================================================================

int main() {
    std::cout << "╔════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║              Prophet.Core 性能基准测试套件                              ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n";
    
    try {
        // 微观基准测试
        micro::benchIndicatorCalculation();
        micro::benchCachePerformance();
        micro::benchSIMDPerformance();
        micro::benchBytecodeExecution();
        
        // 宏观基准测试
        macro::benchSimpleStrategy();
        macro::benchComplexStrategy();
        macro::benchMultiTimeframe();
        
        std::cout << "\n✅ 所有基准测试完成！\n\n";
        
    } catch (const std::exception& e) {
        std::cerr << "❌ 基准测试失败: " << e.what() << "\n";
        return 1;
    }
    
    return 0;
}

