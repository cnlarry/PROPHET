/*
 * ============================================================================
 * 文件名：parser.hpp
 * 功能说明：语法分析器头文件
 * 
 * 定义了语法分析器Parser类
 * 
 * Parser的作用：
 * - 将Token序列（词法单元流）解析成抽象语法树AST
 * - 识别DSL的语法结构（规则、条件、表达式等）
 * - 处理运算符优先级
 * - 支持自定义函数定义和调用
 * 
 * 支持的语法结构：
 * 1. 交易规则：ALL{...} = BUY;
 * 2. 信号函数：ALL、ANY、NONE、MIN、COUNT、MAX、WEIGHTED
 * 3. 条件表达式：比较、逻辑运算、算术运算
 * 4. 参数赋值：$(5m).MACD().period = 12;
 * 5. 自定义函数：myFunc(x, y) { return x + y; }
 * 6. 范围运算：BETWEEN、IN
 * 
 * 解析方法：
 * - 递归下降解析（Recursive Descent Parsing）
 * - 运算符优先级爬升（Operator Precedence）
 * ============================================================================
 */

#pragma once

#include "lexer.hpp"
#include "ast.hpp"
#include "custom_function_ast.hpp"
#include "function_registry.hpp"
#include <vector>
#include <memory>

namespace prophet::dsl {

/**
 * 语法解析器
 *
 * 将 Token 流转换为 AST
 */
class Parser {
public:
    explicit Parser(const std::vector<Token>& tokens, const std::string& source = "");

    // 解析多条规则（用 ; 分隔）
    std::vector<RuleNode> parseRules();

    // 解析单条规则
    RuleNode parseRule();

    // v3.0.2: 解析赋值语句
    AssignmentStatement parseAssignment();

    // v3.0.2: 解析顶层语句（规则或赋值）
    // v4.0: 扩展支持自定义函数
    // v4.1: 扩展支持全局变量声明和赋值
    struct Statement {
        enum Type { 
            RULE,           // 信号规则
            ASSIGNMENT,     // 参数赋值（如 $(5m).MACD().PERIOD = 12）
            FUNCTION_DEF,   // 函数定义
            VAR_DECL,       // 🆕 全局变量声明
            VAR_ASSIGN      // 🆕 全局变量赋值
        } type;
        
        RuleNode rule;
        AssignmentStatement assignment;
        std::shared_ptr<FunctionDefinitionNode> function_def;
        std::shared_ptr<VariableDeclarationNode> var_decl;     // 🆕 变量声明
        std::shared_ptr<AssignmentStatementNode> var_assign;   // 🆕 变量赋值
        
        Statement(RuleNode r) 
            : type(RULE), rule(r), assignment(nullptr), function_def(nullptr), var_decl(nullptr), var_assign(nullptr) {}
        Statement(AssignmentStatement a) 
            : type(ASSIGNMENT), rule(nullptr, "HOLD"), assignment(a), function_def(nullptr), var_decl(nullptr), var_assign(nullptr) {}
        Statement(std::shared_ptr<FunctionDefinitionNode> f) 
            : type(FUNCTION_DEF), rule(nullptr, "HOLD"), assignment(nullptr), function_def(f), var_decl(nullptr), var_assign(nullptr) {}
        Statement(std::shared_ptr<VariableDeclarationNode> vd, bool /*is_decl*/) 
            : type(VAR_DECL), rule(nullptr, "HOLD"), assignment(nullptr), function_def(nullptr), var_decl(vd), var_assign(nullptr) {}
        Statement(std::shared_ptr<AssignmentStatementNode> va) 
            : type(VAR_ASSIGN), rule(nullptr, "HOLD"), assignment(nullptr), function_def(nullptr), var_decl(nullptr), var_assign(va) {}
    };
    
    std::vector<Statement> parseStatements();
    
    // v4.0: 获取函数注册表（引用）
    FunctionRegistry& get_function_registry() { return function_registry_; }
    
    // v4.0: 移动函数注册表（用于Engine存储）
    FunctionRegistry move_function_registry() { return std::move(function_registry_); }

private:
    std::vector<Token> tokens_;
    size_t position_;
    std::string source_;  // 源代码字符串（用于计算行号和列号）
    FunctionRegistry function_registry_;  // v4.0: 函数注册表
    std::vector<std::string> current_function_parameters_;  // v4.0: 当前正在解析的函数的参数列表

    // 当前 token
    const Token& current() const;

    // 前进到下一个 token
    void advance();

    // 检查当前 token 类型
    bool check(TokenType type) const;

    // 匹配并消费 token
    bool match(TokenType type);

    // 期望特定 token（否则抛异常）
    void expect(TokenType type, const std::string& message);
    
    // 计算当前token位置对应的行号和列号（用于错误报告）
    std::pair<int, int> getCurrentLineColumn() const;

    // 解析 ALL{}
    std::shared_ptr<AllNode> parseAll();

    // 解析 ANY{}
    std::shared_ptr<AnyNode> parseAny();

    // 解析 NONE{}
    std::shared_ptr<NoneNode> parseNone();

    // 解析 MIN(n){}
    std::shared_ptr<MinNode> parseMin();

    // 解析 COUNT(n){}
    std::shared_ptr<CountNode> parseCount();

    // 解析 MAX(n){}
    std::shared_ptr<MaxNode> parseMax();

    // 解析 VOTE(n){}
    std::shared_ptr<VoteNode> parseVote();

    // 解析 WEIGHTED(threshold){}
    std::shared_ptr<WeightedNode> parseWeighted();

    // 解析条件列表
    std::vector<ASTNodePtr> parseConditionList();

    // 解析单个条件
    ASTNodePtr parseCondition();

    // 解析比较表达式
    ASTNodePtr parseComparison();

    // 解析逻辑运算符（按优先级从低到高）
    ASTNodePtr parseLogicalOr();     // OR 运算符（优先级最低）
    ASTNodePtr parseLogicalAnd();    // AND 运算符（优先级中等）
    ASTNodePtr parseLogicalNot();    // NOT 运算符和比较表达式（优先级最高）

    // 解析算术表达式
    ASTNodePtr parseArithmetic();
    ASTNodePtr parseTerm();
    ASTNodePtr parseFactor();
    ASTNodePtr parsePrimary();

    // 解析范围运算符
    ASTNodePtr parseBetween(ASTNodePtr left, bool negate = false);
    ASTNodePtr parseIn(ASTNodePtr left, bool negate = false);

    // 解析 NOT()
    ASTNodePtr parseNot();

    // 解析指标引用：$(timeframe).INDICATOR(params).field (新语法)
    ASTNodePtr parseNewIndicatorRef();

    // 解析数据函数：KLINE(timeframe).field(offset)
    ASTNodePtr parseDataFunction();

    // 解析时间序列函数：FUNDINGRATE().method(params)
    ASTNodePtr parseTimeSeriesFunction();

    // 解析CONSECUTIVE序列条件函数：CONSEC(timeframe).field(periods).condition(params)
    ASTNodePtr parseConsecutive();

    // 解析COUNT数据函数：COUNT(timeframe).field(periods).condition(params)
    ASTNodePtr parseCountDataFunction(const std::string& timeframe);

    // 解析数学函数：ABS/MAX/MIN/ROUND
    ASTNodePtr parseMathFunc(const std::string& func_name);

    // 已废弃：解析参数引用（保留以防兼容性问题）
    ASTNodePtr parseParamRef(const std::string& full_ref);

    // 辅助函数：判断名称是否为参数（通过命名约定）
    // 规则：全大写 + 包含下划线 = 参数，否则为字段
    bool isParameter(const std::string& name) const;
    
    // 辅助函数：判断字符串是否为有效的时间框架
    // 规则：1m, 5m, 15m, 30m, 1h, 4h, 1d, 1w等格式
    bool isTimeframe(const std::string& str) const;

    // ========================================
    // v4.0: 自定义函数解析方法
    // ========================================
    
    // 解析函数定义
    std::shared_ptr<FunctionDefinitionNode> parseFunctionDefinition();
    
    // 解析函数参数列表
    std::vector<FunctionParameter> parseFunctionParameters();
    
    // 解析函数体（语句列表）
    std::vector<StatementNodePtr> parseFunctionBody();
    
    // 解析单个语句（if/return/变量声明/变量赋值/表达式语句）
    StatementNodePtr parseStatement();
    
    // 解析变量声明：@var: Type = expr 或 @var = expr（函数内部）
    StatementNodePtr parseVariableDeclaration();
    
    // 🆕 v4.1: 解析顶层变量声明（全局变量）
    std::shared_ptr<VariableDeclarationNode> parseTopLevelVariableDeclaration();
    
    // 🆕 v4.1: 解析顶层变量赋值（全局变量）
    std::shared_ptr<AssignmentStatementNode> parseTopLevelVariableAssignment(const std::string& var_name);
    
    // 解析变量赋值：@var = expr 或 @var += expr
    StatementNodePtr parseVariableAssignment(const std::string& var_name);
    
    // 解析 if-else 语句
    StatementNodePtr parseIfStatement();
    
    // 解析 return 语句
    StatementNodePtr parseReturnStatement();
    
    // 解析用户变量引用：@myvar
    ASTNodePtr parseUserVar();
    
    // 解析三元表达式：condition ? true_expr : false_expr
    ASTNodePtr parseTernaryExpression(ASTNodePtr condition);
    
    // 解析函数调用：functionName(args)
    ASTNodePtr parseFunctionCall(const std::string& func_name);
    
    // 解析类型名称
    std::optional<DeclaredType> parseTypeName();
    
    // 辅助函数：判断标识符是否为函数名
    bool isFunctionName(const std::string& name) const;
    
    // 辅助函数：判断标识符是否为时间序列函数
    bool isTimeSeriesFunction(const std::string& name) const;
};

} // namespace prophet::dsl
