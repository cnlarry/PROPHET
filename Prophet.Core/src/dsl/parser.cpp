/*
 * ============================================================================
 * 文件名：parser.cpp
 * 功能说明：语法分析器（Parser）实现
 * 
 * 什么是语法分析？
 *   把词法分析器产生的Token序列，组织成有意义的语法结构的过程
 *   就像把一个个"单词"组织成"句子"和"段落"
 * 
 * 举个例子：
 *   输入Token序列：[ALL, LBRACE, IDENTIFIER(MACD), GT, NUMBER(0), RBRACE, EQ, BUY]
 *   语法分析后得到的结构：
 *     RuleNode {
 *       signal_func: AllNode { conditions: [MACD > 0] }
 *       action: BUY
 *     }
 * 
 * 主要功能：
 * 1. 解析交易规则（如 ALL{...} = BUY）
 * 2. 解析条件表达式（如 MACD > 0）
 * 3. 解析参数赋值（如 $(5m).MACD().period = 12）
 * 4. 解析自定义函数定义
 * 5. 构建抽象语法树（AST - Abstract Syntax Tree）
 * 
 * 为什么需要语法分析？
 * - Token只是一个个"单词"，没有结构
 * - 需要理解Token之间的关系和层次
 * - 构建可以执行的程序结构
 * 
 * 简单理解：
 * - 词法分析器识别"单词"
 * - 语法分析器理解"语法"，组织成"句子"
 * - 就像理解一句话的主谓宾结构
 * ============================================================================
 */

#include "prophet/dsl/parser.hpp"
#include <regex>
#include <iostream>
#include <sstream>
#include <unordered_set>

namespace prophet::dsl {

// ============================================================================
// 构造函数 - 创建语法分析器
// 参数：
//   tokens - 词法分析器产生的Token序列
//   source - 源代码字符串（用于计算行号和列号，可选）
// ============================================================================
Parser::Parser(const std::vector<Token>& tokens, const std::string& source)
    : tokens_(tokens)      // 保存Token序列
    , position_(0)         // 当前解析位置从0开始
    , source_(source)      // 保存源代码字符串（用于错误报告）
{}

// ========== 基础辅助函数 ==========

// 获取当前位置的Token
const Token& Parser::current() const {
    if (position_ >= tokens_.size()) {
        static Token eof(TokenType::END_OF_FILE, "", position_);
        return eof;
    }
    return tokens_[position_];
}

// 前进到下一个Token
void Parser::advance() {
    if (position_ < tokens_.size()) {
        position_++;
    }
}

// 检查当前Token是否为指定类型
bool Parser::check(TokenType type) const {
    return current().type == type;
}

// 如果当前Token是指定类型，则消费它并返回true
bool Parser::match(TokenType type) {
    if (check(type)) {
        advance();
        return true;
    }
    return false;
}

// 期望当前Token为指定类型，否则抛出异常
void Parser::expect(TokenType type, const std::string& message) {
    if (!check(type)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException(message + ", got: " + current().value, line, col);
    }
    advance();
}

// 计算当前token位置对应的行号和列号（用于错误报告）
std::pair<int, int> Parser::getCurrentLineColumn() const {
    if (position_ >= tokens_.size() || source_.empty()) {
        return std::make_pair(0, 0);  // 无法计算位置
    }
    
    const Token& token = tokens_[position_];
    size_t pos = token.position;
    
    int line = 1;
    int column = 1;
    
    for (size_t i = 0; i < pos && i < source_.length(); i++) {
        if (source_[i] == '\n') {
            line++;
            column = 1;
        } else {
            column++;
        }
    }
    
    return std::make_pair(line, column);
}

std::vector<RuleNode> Parser::parseRules() {
    std::vector<RuleNode> rules;

    while (!check(TokenType::END_OF_FILE)) {
        rules.push_back(parseRule());

        // 跳过分号分隔符
        if (check(TokenType::SEMICOLON)) {
            advance();
        }
    }

    return rules;
}

// ============================================================================
// 函数：parseStatements
// 功能：解析顶层语句（核心入口函数）
// 
// 支持的语句类型：
// 1. 交易规则：ALL{...} = BUY;
// 2. 参数赋值：$(5m).MACD().period = 12;
// 3. 自定义函数定义：myFunc(x, y) { return x + y; }
// 
// 返回值：
//   Statement数组，每个Statement可以是规则、赋值或函数定义
// 
// 解析策略：
//   通过"向前看"（lookahead）判断当前语句的类型
//   - 如果以标识符开头且后面跟(，可能是函数定义
//   - 如果以$.开头，是参数赋值
//   - 否则是交易规则
// ============================================================================
std::vector<Parser::Statement> Parser::parseStatements() {
    std::vector<Statement> statements;

    while (!check(TokenType::END_OF_FILE)) {
        // ========== 向前看：判断语句类型 ==========
        
        // 情况1：检查是否为函数定义
        // 函数定义格式：functionName(params) { body }
        if (check(TokenType::IDENTIFIER)) {
            // 保存当前位置（用于回退）
            size_t saved_pos = position_;
            std::string identifier = current().value;
            advance();
            
            // 如果标识符后面跟着 (，可能是函数定义
            if (check(TokenType::LPAREN)) {
                // 回退到标识符位置
                position_ = saved_pos;
                
                try {
                    // 尝试解析为函数定义
                    auto func_def = parseFunctionDefinition();
                    // 注册函数到函数注册表
                    function_registry_.register_function(func_def->name, func_def);
                    statements.push_back(Statement(func_def));
                    continue;
                } catch (const std::exception& e) {
                    // v4.0: 记录详细错误（用于调试）
                    std::string error_msg = std::string("Failed to parse function definition: ") + e.what();
                    // 输出到控制台以便调试
                    #ifdef _DEBUG
                    std::cerr << "[DEBUG] " << error_msg << std::endl;
                    #endif
                    // 解析失败，回退
                    position_ = saved_pos;
                }
            } else {
                // 不是函数定义，回退
                position_ = saved_pos;
            }
        }
        
        // 情况2：检查是否为参数赋值
        // 参数赋值格式：$(timeframe).INDICATOR.PARAM_NAME = value;
        if (check(TokenType::INDICATOR_REF_START)) {
            size_t saved_pos = position_;
            
            try {
                // 尝试解析为参数赋值
                auto assignment = parseAssignment();
                statements.push_back(Statement(assignment));
                continue;
            } catch (...) {
                // 解析失败，回退并尝试解析为规则
                position_ = saved_pos;
            }
        }
        
        // 🆕 情况3：检查是否为全局变量声明或赋值
        // 格式：@var: Type = expr; 或 @var = expr;
        if (check(TokenType::USER_VAR)) {
            size_t saved_pos = position_;
            std::string var_name = current().value;
            advance();
            
            // 检查是声明还是赋值
            if (check(TokenType::COLON)) {
                // 变量声明：@var: Type = expr; 或 @var: Type;
                position_ = saved_pos;  // 回退
                auto var_decl = parseTopLevelVariableDeclaration();
                statements.push_back(Statement(var_decl, true));  // true 表示 VAR_DECL
                
                // 跳过分号
                if (check(TokenType::SEMICOLON)) {
                    advance();
                }
                continue;
            } else if (check(TokenType::EQ) || check(TokenType::ADD_ASSIGN) || 
                       check(TokenType::SUB_ASSIGN) || check(TokenType::MUL_ASSIGN) ||
                       check(TokenType::DIV_ASSIGN)) {
                // 变量赋值：@var = expr;
                auto var_assign = parseTopLevelVariableAssignment(var_name);
                statements.push_back(Statement(var_assign));
                
                // 跳过分号
                if (check(TokenType::SEMICOLON)) {
                    advance();
                }
                continue;
            } else {
                // 不是变量声明或赋值，回退
                position_ = saved_pos;
            }
        }
        
        // 情况4：解析为交易规则
        // 规则格式：ALL{...} = BUY;
        auto rule = parseRule();
        statements.push_back(Statement(rule));

        // 跳过分号分隔符
        if (check(TokenType::SEMICOLON)) {
            advance();
        }
    }

    return statements;
}

// v3.0.2: 解析赋值语句（新语法）
AssignmentStatement Parser::parseAssignment() {
    // $(timeframe).INDICATOR.PARAM_NAME = value;
    
    // 期望 INDICATOR_REF_START ($)
    if (!check(TokenType::INDICATOR_REF_START)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected '$(timeframe).INDICATOR.PARAM_NAME = value' for parameter assignment", line, col);
    }
    advance(); // consume $
    
    // 期望 (
    expect(TokenType::LPAREN, "Expected '(' after '$' in parameter assignment");
    
    // 期望时间框架
    if (!check(TokenType::IDENTIFIER) && !check(TokenType::NUMBER)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected timeframe in parameter assignment", line, col);
    }
    std::string timeframe = current().value;
    advance();
    
    // 期望 )
    expect(TokenType::RPAREN, "Expected ')' after timeframe in parameter assignment");
    
    // 期望 .
    expect(TokenType::DOT, "Expected '.' after timeframe in parameter assignment");
    
    // 期望指标名称
    if (!check(TokenType::IDENTIFIER)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected indicator name in parameter assignment", line, col);
    }
    std::string indicator = current().value;
    advance();
    
    // 期望 .
    expect(TokenType::DOT, "Expected '.' after indicator name in parameter assignment");
    
    // 期望参数名称
    if (!check(TokenType::IDENTIFIER)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected parameter name in parameter assignment", line, col);
    }
    std::string param_name = current().value;
    advance();
    
    // 检查是否为参数（必须符合命名约定：全大写+下划线）
    if (!isParameter(param_name)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Cannot assign to read-only field '" + param_name + 
                            "'. Only parameters (e.g., FAST_PERIOD, period) can be modified.", line, col);
    }
    
    // 期望 =
    expect(TokenType::EQ, "Expected '=' in parameter assignment");
    
    // 解析右侧表达式
    auto value_expr = parseArithmetic();
    
    // 期望分号
    expect(TokenType::SEMICOLON, "Expected ';' after parameter assignment");
    
    // 创建赋值节点
    auto assignment_node = std::make_shared<AssignmentNode>(indicator, timeframe, param_name, value_expr);
    
    return AssignmentStatement(assignment_node);
}

// ============================================================================
// 函数：parseRule
// 功能：解析交易规则
// 
// 规则格式：
//   信号函数 = 交易动作;
//   例如：ALL{MACD > 0, RSI < 70} = BUY;
// 
// 支持的信号函数：
//   - ALL：所有条件都满足
//   - ANY：任一条件满足
//   - NONE：所有条件都不满足
//   - MIN(n)：至少n个条件满足
//   - COUNT(n)：恰好n个条件满足
//   - MAX(n)：最多n个条件满足
//   - WEIGHTED(threshold)：加权条件，总权重达到阈值
// 
// 支持的交易动作：
//   - BUY：买入
//   - SELL：卖出
//   - HOLD：持有
// ============================================================================
RuleNode Parser::parseRule() {
    // 第1步：解析信号表达式
    std::shared_ptr<ASTNode> signal_expr;

    // 根据关键字判断信号函数类型
    if (check(TokenType::ALL)) {
        signal_expr = parseAll();
    } else if (check(TokenType::ANY)) {
        signal_expr = parseAny();
    } else if (check(TokenType::NONE)) {
        signal_expr = parseNone();
    } else if (check(TokenType::MIN)) {
        signal_expr = parseMin();
    } else if (check(TokenType::COUNT)) {
        signal_expr = parseCount();
    } else if (check(TokenType::MAX)) {
        signal_expr = parseMax();
    } else if (check(TokenType::VOTE)) {
        signal_expr = parseVote();
    } else if (check(TokenType::WEIGHTED)) {
        signal_expr = parseWeighted();
    } else {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected signal function (ALL/ANY/NONE/MIN/COUNT/MAX/VOTE/WEIGHTED)", line, col);
    }

    // 第2步：期望等号
    expect(TokenType::EQ, "Expected '=' after signal expression");

    // 第3步：解析交易动作
    std::string action;
    if (match(TokenType::BUY)) {
        action = "BUY";      // 买入信号
    } else if (match(TokenType::SELL)) {
        action = "SELL";     // 卖出信号
    } else if (match(TokenType::HOLD)) {
        action = "HOLD";     // 持有信号
    } else {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected BUY/SELL/HOLD after '='", line, col);
    }

    // 第4步：解析可选的止盈止损参数
    // 支持的格式：
    //   BUY              -> 止盈止损都是0
    //   BUY(tp)          -> 止盈是tp，止损是0
    //   BUY(tp, sl)      -> 止盈是tp，止损是sl
    std::shared_ptr<ASTNode> tp_expr = nullptr;
    std::shared_ptr<ASTNode> sl_expr = nullptr;
    
    if (check(TokenType::LPAREN)) {
        advance();  // 消费 '('
        
        // 解析第一个参数（止盈）- 使用算术表达式解析
        tp_expr = parseArithmetic();
        
        // 检查是否有第二个参数（止损）
        if (match(TokenType::COMMA)) {
            // 有第二个参数
            sl_expr = parseArithmetic();
        }
        
        // 期望右括号
        expect(TokenType::RPAREN, "Expected ')' after signal parameters");
    }

    // 第5步：期望分号结束规则
    expect(TokenType::SEMICOLON, "Expected ';' after rule");

    // 第6步：创建规则节点（带止盈止损表达式）
    return RuleNode(signal_expr, action, tp_expr, sl_expr);
}

// ============================================================================
// 函数：parseAll
// 功能：解析ALL信号函数
// 
// 格式：ALL { condition1, condition2, ... }
// 语义：所有条件都必须满足，规则才触发
// 
// 例子：
//   ALL { $(5m).MACD().trend = BULLISH, $(5m).RSI().value < 70 }
//   只有当MACD趋势为看涨且RSI小于70时，才触发
// ============================================================================
std::shared_ptr<AllNode> Parser::parseAll() {
    advance(); // 消费 'ALL' 关键字

    expect(TokenType::LBRACE, "Expected '{' after ALL");
    auto conditions = parseConditionList();  // 解析条件列表
    expect(TokenType::RBRACE, "Expected '}' after conditions");

    // 验证条件列表不为空
    if (conditions.empty()) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("ALL{} requires at least one condition", line, col);
    }

    return std::make_shared<AllNode>(conditions);
}

std::shared_ptr<AnyNode> Parser::parseAny() {
    advance(); // consume 'ANY'

    expect(TokenType::LBRACE, "Expected '{' after ANY");
    auto conditions = parseConditionList();
    expect(TokenType::RBRACE, "Expected '}' after conditions");

    // 验证条件列表不为空
    if (conditions.empty()) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("ANY{} requires at least one condition", line, col);
    }

    return std::make_shared<AnyNode>(conditions);
}

std::shared_ptr<NoneNode> Parser::parseNone() {
    advance(); // consume 'NONE'

    expect(TokenType::LBRACE, "Expected '{' after NONE");
    auto conditions = parseConditionList();
    expect(TokenType::RBRACE, "Expected '}' after conditions");

    // 验证条件列表不为空
    if (conditions.empty()) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("NONE{} requires at least one condition", line, col);
    }

    return std::make_shared<NoneNode>(conditions);
}

std::shared_ptr<MinNode> Parser::parseMin() {
    advance(); // consume 'MIN'

    expect(TokenType::LPAREN, "Expected '(' after MIN");

    if (!check(TokenType::NUMBER)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected number after MIN(", line, col);
    }
    int min_count = static_cast<int>(std::stod(current().value));
    advance();

    expect(TokenType::RPAREN, "Expected ')' after MIN count");
    expect(TokenType::LBRACE, "Expected '{' after MIN(n)");

    auto conditions = parseConditionList();

    expect(TokenType::RBRACE, "Expected '}' after conditions");

    // 参数验证：min_count 必须大于 0 且不超过条件总数
    if (min_count <= 0) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("MIN(n): n must be greater than 0", line, col);
    }
    if (min_count > static_cast<int>(conditions.size())) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("MIN(n): n cannot exceed number of conditions", line, col);
    }

    return std::make_shared<MinNode>(min_count, conditions);
}

std::shared_ptr<CountNode> Parser::parseCount() {
    advance(); // consume 'COUNT'

    expect(TokenType::LPAREN, "Expected '(' after COUNT");
    
    if (check(TokenType::NUMBER)) {
        // COUNT(n){} - 信号函数
        int target_count = static_cast<int>(std::stod(current().value));
        advance();

        expect(TokenType::RPAREN, "Expected ')' after COUNT count");
        expect(TokenType::LBRACE, "Expected '{' after COUNT(n)");

        auto conditions = parseConditionList();
        
        // 参数验证：target_count 必须大于等于 0 且不超过条件总数
        if (target_count < 0) {
            throw ParserException("COUNT(n): n cannot be negative");
        }
        if (target_count > static_cast<int>(conditions.size())) {
            throw ParserException("COUNT(n): n cannot exceed number of conditions");
        }

        expect(TokenType::RBRACE, "Expected '}' after conditions");

        return std::make_shared<CountNode>(target_count, conditions);
    }
    throw ParserException("Expected number after COUNT(");
}

std::shared_ptr<MaxNode> Parser::parseMax() {
    advance(); // consume 'MAX'

    expect(TokenType::LPAREN, "Expected '(' after MAX");

    if (!check(TokenType::NUMBER)) {
        throw ParserException("Expected number after MAX(");
    }
    int max_count = static_cast<int>(std::stod(current().value));
    advance();

    expect(TokenType::RPAREN, "Expected ')' after MAX count");
    expect(TokenType::LBRACE, "Expected '{' after MAX(n)");

    auto conditions = parseConditionList();

    expect(TokenType::RBRACE, "Expected '}' after conditions");

    return std::make_shared<MaxNode>(max_count, conditions);
}

std::shared_ptr<VoteNode> Parser::parseVote() {
    advance(); // consume 'VOTE'

    expect(TokenType::LPAREN, "Expected '(' after VOTE");

    if (!check(TokenType::NUMBER)) {
        throw ParserException("Expected number after VOTE(");
    }
    double threshold = std::stod(current().value);
    advance();

    expect(TokenType::RPAREN, "Expected ')' after VOTE threshold");
    expect(TokenType::LBRACE, "Expected '{' after VOTE(n)");

    auto conditions = parseConditionList();

    expect(TokenType::RBRACE, "Expected '}' after conditions");

    // 参数验证
    if (threshold <= 0.0) {
        throw ParserException("VOTE(n): n must be greater than 0");
    }

    // 如果是整数模式（>1），不能超过条件总数
    if (threshold > 1.0 && threshold > static_cast<double>(conditions.size())) {
        throw ParserException("VOTE(n): n cannot exceed number of conditions");
    }

    // 如果是百分比模式（0-1），必须在合法范围
    if (threshold > 0.0 && threshold <= 1.0 && conditions.empty()) {
        throw ParserException("VOTE(n): percentage mode requires at least one condition");
    }

    return std::make_shared<VoteNode>(threshold, conditions);
}

std::shared_ptr<WeightedNode> Parser::parseWeighted() {
    advance(); // consume 'WEIGHTED'

    expect(TokenType::LPAREN, "Expected '(' after WEIGHTED");

    if (!check(TokenType::NUMBER)) {
        throw ParserException("Expected threshold number after WEIGHTED(");
    }
    double threshold = std::stod(current().value);
    
    // 参数验证：threshold 必须在 [0.0, 1.0] 范围内
    if (threshold < 0.0 || threshold > 1.0) {
        throw ParserException("WEIGHTED(threshold): threshold must be between 0.0 and 1.0");
    }
    
    advance();

    expect(TokenType::RPAREN, "Expected ')' after WEIGHTED threshold");
    expect(TokenType::LBRACE, "Expected '{' after WEIGHTED(threshold)");

    // 解析 WEIGHT(condition1, condition2, ...) = weight, ...
    std::vector<WeightedCondition> conditions;

    while (!check(TokenType::RBRACE) && !check(TokenType::END_OF_FILE)) {
        // 期望 WEIGHT
        expect(TokenType::WEIGHT, "Expected WEIGHT in WEIGHTED block");
        expect(TokenType::LPAREN, "Expected '(' after WEIGHT");

        // 解析条件列表（支持多个条件，逗号分隔）
        std::vector<ASTNodePtr> weight_conditions;

        while (!check(TokenType::RPAREN) && !check(TokenType::END_OF_FILE)) {
            auto condition = parseCondition();
            weight_conditions.push_back(condition);

            // 如果下一个是逗号且之后不是右括号，继续解析条件
            if (check(TokenType::COMMA)) {
                advance();
                // 如果逗号后是右括号，说明是 WEIGHT 列表的分隔符，不是条件分隔符
                if (check(TokenType::RPAREN)) {
                    break;
                }
            } else {
                // 没有逗号，结束条件列表
                break;
            }
        }

        expect(TokenType::RPAREN, "Expected ')' after WEIGHT conditions");
        expect(TokenType::EQ, "Expected '=' after WEIGHT(...)");

        // 解析权重
        if (!check(TokenType::NUMBER)) {
            throw ParserException("Expected weight number after WEIGHT(...)=");
        }
        double weight = std::stod(current().value);
        
        // 参数验证：weight 必须在 [0.0, 1.0] 范围内
        if (weight < 0.0 || weight > 1.0) {
            throw ParserException("WEIGHT(...) = weight: weight must be between 0.0 and 1.0");
        }
        
        advance();

        conditions.push_back({weight_conditions, weight});

        // 分隔符：支持 ';' 或 ',' 作为 WEIGHT 语句分隔
        if (check(TokenType::SEMICOLON) || check(TokenType::COMMA)) {
            advance();
        }
    }

    expect(TokenType::RBRACE, "Expected '}' after WEIGHTED conditions");

    return std::make_shared<WeightedNode>(threshold, conditions);
}

std::vector<ASTNodePtr> Parser::parseConditionList() {
    std::vector<ASTNodePtr> conditions;

    while (!check(TokenType::RBRACE) && !check(TokenType::END_OF_FILE)) {
        conditions.push_back(parseCondition());

        // 分隔符：支持 ';' 或 ','；允许最后一个省略
        if (check(TokenType::SEMICOLON) || check(TokenType::COMMA)) {
            advance();
        } else if (!check(TokenType::RBRACE)) {
            throw ParserException("Expected ';' or ',' after condition");
        }
    }

    return conditions;
}

// ============================================================================
// 函数：parseCondition
// 功能：解析单个条件表达式
// 
// 条件可以是：
//   - 比较表达式：MACD > 0
//   - 逻辑表达式：MACD > 0 AND RSI < 70
//   - 范围表达式：RSI BETWEEN(30, 70)
//   - 函数调用结果
// 
// 注意：
//   条件块内不允许参数赋值，参数赋值只能在顶层
// ============================================================================
ASTNodePtr Parser::parseCondition() {
    // 检测并禁止在条件块内进行参数赋值
    // 参数赋值只能在顶层出现，条件块内只能是纯条件表达式
    if (check(TokenType::INDICATOR_REF_START)) {
        size_t saved_pos = position_;
        
        try {
            advance(); // skip $
            
            if (match(TokenType::LPAREN)) {
                // Skip timeframe
                std::string timeframe;
                if (check(TokenType::IDENTIFIER) || check(TokenType::NUMBER)) {
                    timeframe = current().value;
                    advance();
                }
                    
                if (match(TokenType::RPAREN) && match(TokenType::DOT)) {
                    if (check(TokenType::IDENTIFIER)) {
                        std::string indicator = current().value;
                        advance();
                        
                        if (match(TokenType::DOT)) {
                            if (check(TokenType::IDENTIFIER)) {
                                std::string name = current().value;
                                advance();
                                
                                // 如果是参数名且后面跟着 =，则报错
                                if (isParameter(name) && check(TokenType::EQ)) {
                                    throw ParserException(
                                        "Parameter assignment is not allowed inside condition blocks. "
                                        "Move '$(" + timeframe + ")." + indicator + "." + name + " = ...' to the top level before the rule."
                                    );
                                }
                            }
                        }
                    }
                }
            }
        } catch (const ParserException&) {
            throw; // 重新抛出参数赋值错误
        } catch (...) {
            // 其他错误，回退继续正常解析
        }
        
        // 回退到起始位置，继续正常解析条件
        position_ = saved_pos;
    }
    
    // 解析逻辑表达式，从最低优先级开始
    return parseLogicalOr();
}

// ============================================================================
// 逻辑运算符解析（按优先级从低到高）
// 
// 运算符优先级：
//   1. OR（或）- 最低优先级
//   2. AND（与）- 中等优先级
//   3. NOT（非）- 最高优先级
// 
// 例如：A OR B AND C 解析为 A OR (B AND C)
// ============================================================================

// 解析 OR 运算符（最低优先级）
ASTNodePtr Parser::parseLogicalOr() {
    auto left = parseLogicalAnd();

    // 处理连续的OR运算
    while (check(TokenType::OR)) {
        advance();
        auto right = parseLogicalAnd();
        left = std::make_shared<BinaryOpNode>(left, BinaryOpNode::OpType::OR, right);
    }

    return left;
}

// 解析 AND 运算符（中等优先级）
ASTNodePtr Parser::parseLogicalAnd() {
    auto left = parseLogicalNot();

    // 处理连续的AND运算
    while (check(TokenType::AND)) {
        advance();
        auto right = parseLogicalNot();
        left = std::make_shared<BinaryOpNode>(left, BinaryOpNode::OpType::AND, right);
    }

    return left;
}

// 解析 NOT 运算符和比较表达式（最高优先级）
ASTNodePtr Parser::parseLogicalNot() {
    // 如果是NOT运算符，解析NOT表达式
    if (check(TokenType::NOT)) {
        return parseNot();
    }

    // 否则解析比较表达式
    return parseComparison();
}

// ============================================================================
// 函数：parseComparison
// 功能：解析比较表达式
// 
// 支持的比较运算符：
//   - ==, = ：等于
//   - !=    ：不等于
//   - >     ：大于
//   - <     ：小于
//   - >=    ：大于等于
//   - <=    ：小于等于
// 
// 支持的范围运算符：
//   - BETWEEN(min, max)：在范围内
//   - NOT BETWEEN(min, max)：不在范围内
//   - IN(val1, val2, ...)：在列表中
//   - NOT IN(val1, val2, ...)：不在列表中
// ============================================================================
ASTNodePtr Parser::parseComparison() {
    // 先解析左侧表达式
    auto left = parseArithmetic();

    // 情况1：检查比较运算符
    if (check(TokenType::EQ) || check(TokenType::NE) ||
        check(TokenType::GT) || check(TokenType::LT) ||
        check(TokenType::GE) || check(TokenType::LE)) {

        TokenType op = current().type;
        advance();
        auto right = parseArithmetic();  // 解析右侧表达式

        // 将Token类型转换为AST节点的运算符类型
        BinaryOpNode::OpType bin_op;
        switch (op) {
            case TokenType::EQ: bin_op = BinaryOpNode::OpType::EQ; break;  // 等于
            case TokenType::NE: bin_op = BinaryOpNode::OpType::NE; break;  // 不等于
            case TokenType::GT: bin_op = BinaryOpNode::OpType::GT; break;  // 大于
            case TokenType::LT: bin_op = BinaryOpNode::OpType::LT; break;  // 小于
            case TokenType::GE: bin_op = BinaryOpNode::OpType::GE; break;  // 大于等于
            case TokenType::LE: bin_op = BinaryOpNode::OpType::LE; break;  // 小于等于
            default: throw ParserException("Unknown comparison operator");
        }

        // ⭐ 关键：如果left是多时间框架，展开成ALL{}
        if (auto indicator_ref = std::dynamic_pointer_cast<IndicatorRefNode>(left)) {
            if (indicator_ref->hasMultipleTimeframes()) {
                // 为每个时间框架创建一个比较条件
                std::vector<ASTNodePtr> conditions;
                for (const auto& tf : indicator_ref->getTimeframes()) {
                    // 创建单时间框架的IndicatorRefNode
                    auto single_ref = std::make_shared<IndicatorRefNode>(
                        indicator_ref->getIndicator(),
                        tf,
                        indicator_ref->getField(),
                        indicator_ref->getFieldParams(),
                        indicator_ref->getSubfield()
                    );
                    // 创建比较节点
                    auto comparison = std::make_shared<BinaryOpNode>(
                        single_ref, bin_op, right
                    );
                    conditions.push_back(comparison);
                }
                
                // 返回隐式ALL节点
                return std::make_shared<AllNode>(conditions);
            }
        }

        // 正常的单值比较
        return std::make_shared<BinaryOpNode>(left, bin_op, right);
    }

    // 检查范围运算符
    // 处理 NOT BETWEEN / NOT IN（关键字 NOT 或标识符 NOT）
    if (check(TokenType::NOT) || (check(TokenType::IDENTIFIER) && current().value == "NOT")) {
        advance();
        if (check(TokenType::BETWEEN) || (check(TokenType::IDENTIFIER) && current().value == "BETWEEN")) {
            return parseBetween(left, /*negate=*/true);
        }
        if (check(TokenType::IN) || (check(TokenType::IDENTIFIER) && current().value == "IN")) {
            return parseIn(left, /*negate=*/true);
        }
        throw ParserException("Expected BETWEEN or IN after NOT");
    }

    if (check(TokenType::BETWEEN) || (check(TokenType::IDENTIFIER) && current().value == "BETWEEN")) {
        return parseBetween(left, /*negate=*/false);
    }

    if (check(TokenType::IN) || (check(TokenType::IDENTIFIER) && current().value == "IN")) {
        left = parseIn(left, /*negate=*/false);
    }

    // v4.0: 检查三元运算符
    if (check(TokenType::QUESTION)) {
        return parseTernaryExpression(left);
    }

    // 没有运算符：
    // 对于布尔字段的简写（如 RSI(5m).overbought），不再强制包裹成 "= true"，
    // 直接返回左侧表达式，交由求值阶段将其按布尔值处理。
    // 若未来需要显式化（转换为 = true），可在此处包装 BinaryOpNode。
    return left;
}

ASTNodePtr Parser::parseBetween(ASTNodePtr left, bool negate) {
    advance(); // consume 'BETWEEN'

    expect(TokenType::LPAREN, "Expected '(' after BETWEEN");

    auto min = parseArithmetic();

    expect(TokenType::COMMA, "Expected ',' in BETWEEN");

    auto max = parseArithmetic();

    expect(TokenType::RPAREN, "Expected ')' after BETWEEN");

    return std::make_shared<BetweenNode>(left, min, max, negate);
}

ASTNodePtr Parser::parseIn(ASTNodePtr left, bool negate) {
    advance(); // consume 'IN'

    expect(TokenType::LPAREN, "Expected '(' after IN");

    std::vector<ASTNodePtr> values;

    while (!check(TokenType::RPAREN) && !check(TokenType::END_OF_FILE)) {
        values.push_back(parseArithmetic());

        if (check(TokenType::COMMA)) {
            advance();
        } else {
            break;
        }
    }

    expect(TokenType::RPAREN, "Expected ')' after IN values");

    // 验证值列表不为空
    if (values.empty()) {
        throw ParserException("IN() requires at least one value");
    }

    return std::make_shared<InNode>(left, values, negate);
}

ASTNodePtr Parser::parseNot() {
    advance(); // consume 'NOT'

    expect(TokenType::LPAREN, "Expected '(' after NOT");

    auto condition = parseComparison();

    expect(TokenType::RPAREN, "Expected ')' after NOT condition");

    return std::make_shared<NotNode>(condition);
}

ASTNodePtr Parser::parseArithmetic() {
    auto left = parseTerm();

    while (check(TokenType::PLUS) || check(TokenType::MINUS)) {
        TokenType op = current().type;
        advance();
        auto right = parseTerm();

        BinaryOpNode::OpType bin_op = (op == TokenType::PLUS) ?
            BinaryOpNode::OpType::ADD : BinaryOpNode::OpType::SUB;

        left = std::make_shared<BinaryOpNode>(left, bin_op, right);
    }

    return left;
}

ASTNodePtr Parser::parseTerm() {
    auto left = parseFactor();

    while (check(TokenType::MULTIPLY) || check(TokenType::DIVIDE) || check(TokenType::MODULO)) {
        TokenType op = current().type;
        advance();
        auto right = parseFactor();

        BinaryOpNode::OpType bin_op;
        switch (op) {
            case TokenType::MULTIPLY: bin_op = BinaryOpNode::OpType::MUL; break;
            case TokenType::DIVIDE:   bin_op = BinaryOpNode::OpType::DIV; break;
            case TokenType::MODULO:   bin_op = BinaryOpNode::OpType::MOD; break;
            default: throw ParserException("Unknown operator");
        }

        left = std::make_shared<BinaryOpNode>(left, bin_op, right);
    }

    return left;
}

ASTNodePtr Parser::parseFactor() {
    // 一元运算符
    if (check(TokenType::MINUS)) {
        advance();
        auto operand = parseFactor();
        return std::make_shared<UnaryOpNode>(UnaryOpNode::OpType::NEG, operand);
    }

    if (check(TokenType::PLUS)) {
        advance();
        auto operand = parseFactor();
        return std::make_shared<UnaryOpNode>(UnaryOpNode::OpType::POS, operand);
    }

    // 数学函数 - 仅处理已知的数学函数名
    if (check(TokenType::IDENTIFIER)) {
        std::string identifier = current().value;
        // 只对已知的数学函数调用 parseMathFunc，避免与数据函数冲突
        static const std::unordered_set<std::string> math_functions = {
            "ABS", "ROUND", "POW", "SQRT", "EXP", "LOG", "SIN", "COS", "TAN"
        };
        
        if (math_functions.find(identifier) != math_functions.end()) {
            size_t saved_pos = position_;
            advance();
            if (check(TokenType::LPAREN)) {
                position_ = saved_pos;
                return parseMathFunc(identifier);
            }
            position_ = saved_pos;
        }
    }

    // 括号 - 支持嵌套的完整逻辑表达式
    if (check(TokenType::LPAREN)) {
        advance();
        // 调用 parseCondition() 以支持括号内的比较和逻辑运算
        // 例如: ($(5m).MACD().trend = BULLISH AND $(5m).RSI().value < 70)
        auto expr = parseCondition();
        expect(TokenType::RPAREN, "Expected ')' after expression");
        return expr;
    }

    return parsePrimary();
}

ASTNodePtr Parser::parsePrimary() {
    // 数字
    if (check(TokenType::NUMBER)) {
        double value = std::stod(current().value);
        advance();
        return std::make_shared<NumberNode>(value);
    }

    // 布尔值
    if (check(TokenType::BOOLEAN)) {
        bool value = (current().value == "true");
        advance();
        return std::make_shared<BooleanNode>(value);
    }

    // 字符串
    if (check(TokenType::STRING)) {
        std::string value = current().value;
        advance();
        return std::make_shared<StringNode>(value);
    }

    // v4.0: 用户变量 @myvar
    if (check(TokenType::USER_VAR)) {
        return parseUserVar();
    }

    // 指标引用：$(timeframe).INDICATOR(params).field (新语法)
    if (check(TokenType::INDICATOR_REF_START)) {
        advance(); // 跳过 INDICATOR_REF_START token
        return parseNewIndicatorRef();
    }
    

    // ============================================================================
    // COUNT数据函数特殊处理
    // ============================================================================
    // COUNT是关键字(TokenType::COUNT)，需要在这里专门处理
    // 区分两种用法：
    //   1. 信号函数：COUNT(n){...} - 在parseRule()中处理
    //   2. 数据函数：COUNT(tf).field(periods).condition(...) - 在这里处理
    if (check(TokenType::COUNT)) {
        size_t saved_pos = position_;
        advance(); // consume COUNT
        
        // 检查是否为数据函数：COUNT(timeframe)...
        if (check(TokenType::LPAREN)) {
            advance(); // consume (
            
            // 检查括号内是时间框架还是数字
            if (check(TokenType::IDENTIFIER)) {
                std::string potential_tf = current().value;
                
                // 判断是否为时间框架格式
                if (isTimeframe(potential_tf)) {
                    // 这是COUNT数据函数：COUNT(5m).close(20).rising()
                    std::string timeframe = potential_tf;
                    advance(); // consume timeframe
                    
                    expect(TokenType::RPAREN, "Expected ')' after timeframe in COUNT data function");
                    
                    // 检查后面是否为点号（进一步确认是数据函数）
                    if (check(TokenType::DOT)) {
                        // 确认是数据函数，调用parseCountDataFunction
                        return parseCountDataFunction(timeframe);
                    }
                }
            } else if (check(TokenType::NUMBER)) {
                // 这可能是数字格式的时间框架（如：1m, 5m）
                // 需要先获取数字，然后检查是否有单位后缀
                std::string number_str = current().value;
                size_t peek_pos = position_ + 1;
                
                // 前瞻：检查下一个token
                if (peek_pos < tokens_.size()) {
                    const Token& next_token = tokens_[peek_pos];
                    
                    // 如果数字后紧跟标识符（如：1 m），组合为时间框架
                    if (next_token.type == TokenType::IDENTIFIER) {
                        std::string unit = next_token.value;
                        std::string potential_tf = number_str + unit;
                        
                        if (isTimeframe(potential_tf)) {
                            // 这是时间框架：COUNT(1m)...
                            advance(); // consume number
                            advance(); // consume unit
                            
                            expect(TokenType::RPAREN, "Expected ')' after timeframe in COUNT data function");
                            
                            if (check(TokenType::DOT)) {
                                return parseCountDataFunction(potential_tf);
                            }
                        }
                    }
                }
            }
        }
        
        // 如果不符合数据函数的模式，回退
        position_ = saved_pos;
        
        // COUNT信号函数只能在规则顶层使用，不能出现在表达式中
        throw ParserException(
            "COUNT signal function (COUNT(n){...}) must be at rule level, not in expression. "
            "Did you mean to use COUNT data function like COUNT(5m).close(20).rising()?"
        );
    }

    // 标识符：可能是数据函数、自定义函数、函数参数或枚举值
    // CONSECUTIVE和CONSEC现在作为普通数据函数处理
    // 注意：数学函数(ABS, MAX, MIN, ROUND, SQRT, POW)已在 parseFactor() 中处理
    if (check(TokenType::IDENTIFIER)) {
        std::string identifier = current().value;
        size_t saved_pos = position_;
        advance();

        // 如果后面跟着 '('，可能是数据函数或自定义函数调用
        if (check(TokenType::LPAREN)) {
            position_ = saved_pos; // 回退
            
            // 检查是否为CONSECUTIVE或CONSEC
            if (identifier == "CONSECUTIVE" || identifier == "CONSEC") {
                return parseConsecutive();
            }
            
            // v4.0: 检查是否为自定义函数
            if (isFunctionName(identifier)) {
                std::string func_name = current().value;
                advance(); // 消费函数名
                return parseFunctionCall(func_name);
            }
            
            // 检查是否为时间序列函数（FUNDINGRATE, FEARGREED等）
            if (isTimeSeriesFunction(identifier)) {
                return parseTimeSeriesFunction();
            }
            
            // 否则是数据函数
            return parseDataFunction();
        }

        // v4.0: 检查是否为函数参数引用
        for (const auto& param_name : current_function_parameters_) {
            if (identifier == param_name) {
                // 这是对函数参数的引用
                // 将参数引用作为用户变量处理（在运行时从作用域中获取）
                // 注意：函数参数在作用域中存储时不带 @ 前缀
                return std::make_shared<VariableReferenceNode>(identifier);
            }
        }

        // 否则当作枚举值/字符串
        return std::make_shared<StringNode>(identifier);
    }

    auto [line, col] = getCurrentLineColumn();
    throw ParserException("Expected primary expression", line, col);
}

ASTNodePtr Parser::parseNewIndicatorRef() {
    // $(timeframe).INDICATOR(params).field
    // 支持三种形式：
    //   1. $(5m).MACD(12,26,9).trend  - 位置参数
    //   2. $(5m).MACD().trend         - 空参数列表
    //   3. $(5m).MACD().trend          - 无括号
    
    // 步骤1：解析时间框架
    expect(TokenType::LPAREN, "Expected '(' after '$' in indicator reference");
    
    if (!check(TokenType::IDENTIFIER) && !check(TokenType::NUMBER)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected timeframe in indicator reference", line, col);
    }
    std::string timeframe = current().value;
    advance();
    expect(TokenType::RPAREN, "Expected ')' after timeframe");
    
    // 步骤2：期望点号
    expect(TokenType::DOT, "Expected '.' after timeframe");
    
    // 步骤3：解析指标名
    if (!check(TokenType::IDENTIFIER)) {
        auto [line, col] = getCurrentLineColumn();
        throw ParserException("Expected indicator name after '.'", line, col);
    }
    std::string indicator = current().value;
    advance();
    
    // 步骤4：检查是否有括号（参数列表或字段访问）
    bool has_paren = check(TokenType::LPAREN);
    
    if (has_paren) {
        // 有括号：解析参数列表（可选）
        advance(); // consume (
        std::vector<Value> indicator_params;
        
        if (!check(TokenType::RPAREN)) {
            // 解析位置参数列表（必须是常量值）
            do {
                // 解析参数值（必须是数字常量）
                if (check(TokenType::NUMBER)) {
                    double param_value = std::stod(current().value);
                    indicator_params.push_back(Value::fromNumber(param_value));
                    advance();
                } else {
                    throw ParserException("Indicator parameters must be constant numbers (e.g., $(5m).MACD(12,26,9).trend)");
                }
            } while (match(TokenType::COMMA));
        }
        expect(TokenType::RPAREN, "Expected ')' after indicator parameters");
        
        // 期望点号
        expect(TokenType::DOT, "Expected '.' after indicator parameters");
        
        // 解析字段名
        if (!check(TokenType::IDENTIFIER)) {
            throw ParserException("Expected field name after indicator parameters");
        }
        std::string field = current().value;
        advance();
        
        // 检查是否有字段偏移量：FIELD(OFFSET)
        std::vector<ASTNodePtr> field_params;
        if (check(TokenType::LPAREN)) {
            advance(); // consume (
            // 解析偏移量（必须是数字常量，支持负数）
            double offset_value = 0.0;
            bool has_offset = false;
            
            // 处理负号
            bool is_negative = false;
            if (match(TokenType::MINUS)) {
                is_negative = true;
            }
            
            if (check(TokenType::NUMBER)) {
                offset_value = std::stod(current().value);
                if (is_negative) offset_value = -offset_value;
                has_offset = true;
                advance();
                
                // 验证偏移量范围：-100 到 0
                if (offset_value < -100 || offset_value > 0) {
                    auto [line, col] = getCurrentLineColumn();
                    throw ParserException("Field offset must be in range [-100, 0], got: " + std::to_string(offset_value), line, col);
                }
                field_params.push_back(std::make_shared<NumberNode>(offset_value));
            } else if (!check(TokenType::RPAREN)) {
                auto [line, col] = getCurrentLineColumn();
                throw ParserException("Field offset must be a constant number (e.g., histogram(-1))", line, col);
            }
            expect(TokenType::RPAREN, "Expected ')' after field offset");
        }
        
        // 创建指标引用节点
        return std::make_shared<IndicatorRefNode>(indicator, timeframe, field, 
                                                  field_params, "", 
                                                  indicator_params);
    } else {
        // 没有括号：可能是字段访问或参数读取
        // 期望点号
        expect(TokenType::DOT, "Expected '.' after indicator name");
        
        // 解析名称（可能是字段名或参数名）
        if (!check(TokenType::IDENTIFIER)) {
            throw ParserException("Expected field or parameter name after indicator");
        }
        std::string name = current().value;
        advance();
        
        // 检查是否为参数（全大写+下划线）
        if (isParameter(name)) {
            // 这是参数读取：$(timeframe).INDICATOR.PARAM_NAME
            return std::make_shared<ParamRefNode>(indicator, timeframe, name);
        } else {
            // 这是字段访问：$(timeframe).INDICATOR.field
            // 检查是否有字段偏移量：FIELD(OFFSET) 或 FIELD()
            std::vector<ASTNodePtr> field_params;
            if (check(TokenType::LPAREN)) {
                advance(); // consume (
                // 解析偏移量（可选，默认为0，支持负数）
                double offset_value = 0.0;
                bool has_offset = false;
                
                // 处理负号
                bool is_negative = false;
                if (match(TokenType::MINUS)) {
                    is_negative = true;
                }
                
                if (check(TokenType::NUMBER)) {
                    offset_value = std::stod(current().value);
                    if (is_negative) offset_value = -offset_value;
                    has_offset = true;
                    advance();
                    
                    // 验证偏移量范围：-100 到 0
                    if (offset_value < -100 || offset_value > 0) {
                        auto [line, col] = getCurrentLineColumn();
                        throw ParserException("Field offset must be in range [-100, 0], got: " + std::to_string(offset_value), line, col);
                    }
                    field_params.push_back(std::make_shared<NumberNode>(offset_value));
                } else if (!check(TokenType::RPAREN)) {
                    auto [line, col] = getCurrentLineColumn();
                    throw ParserException("Field offset must be a constant number (e.g., histogram(-1))", line, col);
                }
                // FIELD() 等同于 FIELD(0)，不需要特殊处理
                expect(TokenType::RPAREN, "Expected ')' after field offset");
            }
            
            return std::make_shared<IndicatorRefNode>(indicator, timeframe, name, 
                                                      field_params, "", 
                                                      std::vector<Value>());
        }
    }
}


ASTNodePtr Parser::parseDataFunction() {
    // KLINE(timeframe).field(offset)
    // PATTERN(timeframe).pattern_name(penetration)
    // DATA_FUNC(timeframe).field(offset)
    std::string func_name = current().value;
    advance();

    expect(TokenType::LPAREN, "Expected '(' after data function name");

    // 数据函数需要timeframe
    if (!check(TokenType::IDENTIFIER) && !check(TokenType::NUMBER)) {
        auto [line, col] = getCurrentLineColumn();
        std::string context = position_ < tokens_.size() ? 
            (", got: " + current().value) : ", reached end of input";
        throw ParserException("Expected timeframe in " + func_name + "()" + context, line, col);
    }

    std::string timeframe = current().value;
    advance();

    expect(TokenType::RPAREN, "Expected ')' after timeframe");
    expect(TokenType::DOT, "Expected '.' after function(timeframe)");

    if (!check(TokenType::IDENTIFIER)) {
        auto [line, col] = getCurrentLineColumn();
        std::string context = position_ < tokens_.size() ? 
            (", got: " + current().value) : ", reached end of input";
        throw ParserException("Expected field name after " + func_name + "(timeframe)" + context, line, col);
    }

    std::string field = current().value;
    advance();

    // 特殊处理 PATTERN 函数
    if (func_name == "PATTERN") {
        expect(TokenType::LPAREN, "Expected '(' after pattern name");
        
        // 解析 penetration 参数（可选，默认0.3）
        ASTNodePtr penetration = nullptr;
        if (!check(TokenType::RPAREN)) {
            penetration = parseArithmetic();
        }
        
        expect(TokenType::RPAREN, "Expected ')' after pattern parameters");
        
        return std::make_shared<PatternNode>(timeframe, field, penetration);
    }

    // 特殊处理 HT 函数的子字段：HT(5m).PHASOR.inphase
    std::string subfield = "";
    if (func_name == "HT" && check(TokenType::DOT)) {
        advance();  // 消费 '.'
        if (!check(TokenType::IDENTIFIER)) {
            auto [line, col] = getCurrentLineColumn();
            throw ParserException("Expected subfield name after HT." + field + ".", line, col);
        }
        subfield = current().value;
        advance();
        
        // HT函数的子字段不需要参数，直接返回
        return std::make_shared<IndicatorRefNode>(func_name, timeframe, field, std::vector<ASTNodePtr>(), subfield);
    }

    // 特殊处理 PRICE 函数：支持语法糖 PRICE(5m).AVG（无括号）
    if (func_name == "PRICE" && !check(TokenType::LPAREN)) {
        // 没有括号，使用默认offset=0
        return std::make_shared<IndicatorRefNode>(func_name, timeframe, field);
    }

    // 其他数据函数（KLINE, HIGHEST, LOWEST, CHANGE, FVG, ORDERBLOCK等）
    // 增强鲁棒性：支持无括号和空括号两种形式
    // 1. FVG(5m).bullish - 无括号（布尔字段，不需要参数）
    // 2. FVG(5m).bullish() - 空括号（等同于无括号）
    // 3. FVG(5m).top(1) - 带参数（数值字段，可选参数）
    // 4. FVG(5m).top - 无括号（使用默认参数）
    
    std::vector<ASTNodePtr> field_params;
    
    if (check(TokenType::LPAREN)) {
        // 有括号：解析参数（可以为空）
        advance();  // 消费 '('
        
        if (!check(TokenType::RPAREN)) {
            // 有参数：解析参数列表
            do {
                field_params.push_back(parseArithmetic());
            } while (match(TokenType::COMMA));
        }
        
        expect(TokenType::RPAREN, "Expected ')' after field parameters");
    } else {
        // 无括号：使用空参数列表（表示使用默认值）
        // 这允许 FVG(5m).bullish 这种简洁写法
        // 运行时可以根据字段类型决定是否需要参数
    }

    // 检查是否有 subfield 或 submethod（如 CHANGE(5m).close(10).pct 或 SWING(5m).high().value(0)）
    subfield = "";  // 重用之前定义的 subfield 变量
    std::vector<ASTNodePtr> submethod_params;
    
    if (check(TokenType::DOT)) {
        advance();  // 消费 '.'
        if (!check(TokenType::IDENTIFIER)) {
            auto [line, col] = getCurrentLineColumn();
            throw ParserException("Expected subfield/submethod name after " + func_name + "().", line, col);
        }
        subfield = current().value;
        advance();
        
        // 检查 subfield 后面是否有括号（表示是 submethod，需要参数）
        if (check(TokenType::LPAREN)) {
            // 这是 submethod，解析参数
            advance();  // 消费 '('
            
            if (!check(TokenType::RPAREN)) {
                // 有参数：解析参数列表
                do {
                    submethod_params.push_back(parseArithmetic());
                } while (match(TokenType::COMMA));
            }
            
            expect(TokenType::RPAREN, "Expected ')' after submethod parameters");
        }
        // 如果没有括号，就是 subfield（无参数），submethod_params 保持为空
    }

    // 复用 IndicatorRefNode，但使用字段参数、subfield/submethod 和可能的 submethod 参数
    return std::make_shared<IndicatorRefNode>(func_name, timeframe, field, field_params, subfield, std::vector<Value>(), submethod_params);
}

ASTNodePtr Parser::parseMathFunc(const std::string& func_name) {
    // 解析数学函数

    // 消费函数名（可能是 IDENTIFIER 或 MAX/MIN token）
    if (check(TokenType::MAX) || check(TokenType::MIN) || check(TokenType::IDENTIFIER)) {
        advance();
    }

    // 期望 '('
    expect(TokenType::LPAREN, "Expected '(' after math function name");

    // 解析参数
    std::vector<ASTNodePtr> args;
    if (!check(TokenType::RPAREN)) {
        do {
            args.push_back(parseArithmetic());
        } while (match(TokenType::COMMA));
    }

    // 期望 ')'
    expect(TokenType::RPAREN, "Expected ')' after math function arguments");

    return std::make_shared<MathFunctionNode>(func_name, args);
}

ASTNodePtr Parser::parseParamRef(const std::string& full_ref) {
    // 解析：#.RSI(5m).OVERBOUGHT
    // full_ref = "RSI(5m).OVERBOUGHT"

    std::regex param_regex(R"((\w+)\(([^)]+)\)\.(\w+))");
    std::smatch matches;

    if (!std::regex_match(full_ref, matches, param_regex)) {
        throw ParserException("Invalid parameter reference: $" + full_ref);
    }

    std::string indicator = matches[1].str();
    std::string timeframe = matches[2].str();
    std::string param_name = matches[3].str();

    return std::make_shared<ParamRefNode>(indicator, timeframe, param_name);
}

ASTNodePtr Parser::parseConsecutive() {
    // CONSEC(5m).close(3).rising()
    // 重构：使用IndicatorRefNode代替ConsecutiveNode，将逻辑转移到functions/
    
    // 保存函数名（CONSECUTIVE或CONSEC）
    std::string func_name = current().value;
    advance(); // consume CONSECUTIVE or CONSEC
    
    expect(TokenType::LPAREN, "Expected '(' after " + func_name);
    
    // 解析时间框架
    if (!check(TokenType::IDENTIFIER) && !check(TokenType::NUMBER)) {
        throw ParserException("Expected timeframe in " + func_name);
    }
    std::string timeframe = current().value;
    advance();
    
    expect(TokenType::RPAREN, "Expected ')' after timeframe");
    expect(TokenType::DOT, "Expected '.' after " + func_name + "(timeframe)");
    
    // 解析字段名 (close, open, high, low, volume)
    if (!check(TokenType::IDENTIFIER)) {
        throw ParserException("Expected field name after " + func_name + "(timeframe).");
    }
    std::string field = current().value;
    
    // 验证字段名
    if (field != "close" && field != "open" && field != "high" && 
        field != "low" && field != "volume") {
        throw ParserException(
            "Invalid field '" + field + "' for " + func_name + ". " +
            "Supported fields: close, open, high, low, volume"
        );
    }
    advance();
    
    expect(TokenType::LPAREN, "Expected '(' after field name");
    
    // 解析期数
    if (!check(TokenType::NUMBER)) {
        throw ParserException("Expected number of periods in " + func_name);
    }
    int periods = static_cast<int>(std::stod(current().value));
    if (periods < 1 || periods > 100) {
        throw ParserException("period must be in range [1, 100], got: " + std::to_string(periods));
    }
    advance();
    
    expect(TokenType::RPAREN, "Expected ')' after periods");
    expect(TokenType::DOT, "Expected '.' after field(periods)");
    
    // 解析条件类型
    if (!check(TokenType::IDENTIFIER)) {
        throw ParserException("Expected condition name after " + func_name + "(tf).field(periods).");
    }
    std::string condition_name = current().value;
    advance();
    
    // 验证条件名（不需要转换为枚举，直接传递字符串）
    if (condition_name != "rising" && condition_name != "falling" &&
        condition_name != "increasing" && condition_name != "decreasing" &&
        condition_name != "above" && condition_name != "below" &&
        condition_name != "between" && condition_name != "crosses_above" &&
        condition_name != "crosses_below") {
        throw ParserException(
            "Unknown " + func_name + " condition '" + condition_name + "'. " +
            "Supported: rising, falling, increasing, decreasing, above, below, between, crosses_above, crosses_below"
        );
    }
    
    expect(TokenType::LPAREN, "Expected '(' after condition name");
    
    // 解析条件参数（可选）
    std::vector<ASTNodePtr> field_params;
    // 第一个参数是periods
    field_params.push_back(std::make_shared<NumberNode>(periods));
    
    // 后续参数是条件参数（如阈值）
    if (!check(TokenType::RPAREN)) {
        do {
            field_params.push_back(parseArithmetic());
        } while (match(TokenType::COMMA));
    }
    
    expect(TokenType::RPAREN, "Expected ')' after condition parameters");
    
    // 验证参数数量（除了periods）
    size_t condition_param_count = field_params.size() - 1;
    if ((condition_name == "above" || condition_name == "below" ||
         condition_name == "crosses_above" || condition_name == "crosses_below") && 
        condition_param_count != 1) {
        throw ParserException(
            "Condition '" + condition_name + "' requires exactly 1 parameter (threshold)"
        );
    }
    
    if (condition_name == "between" && condition_param_count != 2) {
        throw ParserException(
            "Condition 'between' requires exactly 2 parameters (min, max)"
        );
    }
    
    if ((condition_name == "rising" || condition_name == "falling" ||
         condition_name == "increasing" || condition_name == "decreasing") && 
        condition_param_count != 0) {
        throw ParserException(
            "Condition '" + condition_name + "' does not accept parameters"
        );
    }
    
    // 返回IndicatorRefNode，而不是ConsecutiveNode
    // name: CONSECUTIVE/CONSEC
    // timeframe: 5m
    // field: close
    // field_params: [periods, ...condition_params]
    // subfield: condition_name (rising, above等)
    return std::make_shared<IndicatorRefNode>(func_name, timeframe, field, field_params, condition_name);
}

ASTNodePtr Parser::parseCountDataFunction(const std::string& timeframe) {
    // COUNT(5m).close(20).rising()
    // 重构：使用IndicatorRefNode代替CountDataNode，将逻辑转移到functions/
    // 注意：此时timeframe已经被解析，current token是DOT
    
    expect(TokenType::DOT, "Expected '.' after COUNT(timeframe)");
    
    // 解析字段名 (close, open, high, low, volume)
    if (!check(TokenType::IDENTIFIER)) {
        throw ParserException("Expected field name after COUNT(timeframe).");
    }
    std::string field = current().value;
    
    // 验证字段名
    if (field != "close" && field != "open" && field != "high" && 
        field != "low" && field != "volume") {
        throw ParserException(
            "Invalid field '" + field + "' for COUNT. " +
            "Supported fields: close, open, high, low, volume"
        );
    }
    advance();
    
    expect(TokenType::LPAREN, "Expected '(' after field name");
    
    // 解析期数
    if (!check(TokenType::NUMBER)) {
        throw ParserException("Expected number of periods in COUNT");
    }
    int periods = static_cast<int>(std::stod(current().value));
    if (periods <= 0) {
        throw ParserException("period count must be greater than 0");
    }
    advance();
    
    expect(TokenType::RPAREN, "Expected ')' after periods");
    expect(TokenType::DOT, "Expected '.' after field(periods)");
    
    // 解析条件类型
    if (!check(TokenType::IDENTIFIER)) {
        throw ParserException("Expected condition name after COUNT(tf).field(periods).");
    }
    std::string condition_name = current().value;
    advance();
    
    // 验证条件名（不需要转换为枚举，直接传递字符串）
    if (condition_name != "rising" && condition_name != "falling" &&
        condition_name != "increasing" && condition_name != "decreasing" &&
        condition_name != "above" && condition_name != "below" &&
        condition_name != "between" && condition_name != "crosses_above" &&
        condition_name != "crosses_below") {
        throw ParserException(
            "Unknown COUNT condition '" + condition_name + "'. " +
            "Supported: rising, falling, increasing, decreasing, above, below, between, crosses_above, crosses_below"
        );
    }
    
    expect(TokenType::LPAREN, "Expected '(' after condition name");
    
    // 解析条件参数（可选）
    std::vector<ASTNodePtr> field_params;
    // 第一个参数是periods
    field_params.push_back(std::make_shared<NumberNode>(periods));
    
    // 后续参数是条件参数（如阈值）
    if (!check(TokenType::RPAREN)) {
        do {
            field_params.push_back(parseArithmetic());
        } while (match(TokenType::COMMA));
    }
    
    expect(TokenType::RPAREN, "Expected ')' after condition parameters");
    
    // 验证参数数量（除了periods）
    size_t condition_param_count = field_params.size() - 1;
    if ((condition_name == "above" || condition_name == "below" ||
         condition_name == "crosses_above" || condition_name == "crosses_below") && 
        condition_param_count != 1) {
        throw ParserException(
            "Condition '" + condition_name + "' requires exactly 1 parameter (threshold)"
        );
    }
    
    if (condition_name == "between" && condition_param_count != 2) {
        throw ParserException(
            "Condition 'between' requires exactly 2 parameters (min, max)"
        );
    }
    
    if ((condition_name == "rising" || condition_name == "falling" ||
         condition_name == "increasing" || condition_name == "decreasing") && 
        condition_param_count != 0) {
        throw ParserException(
            "Condition '" + condition_name + "' does not accept parameters"
        );
    }
    
    // 返回IndicatorRefNode，而不是CountDataNode
    // name: COUNT_DATA（避免与信号函数COUNT冲突）
    // timeframe: 5m
    // field: close
    // field_params: [periods, ...condition_params]
    // subfield: condition_name (rising, above等)
    return std::make_shared<IndicatorRefNode>("COUNT_DATA", timeframe, field, field_params, condition_name);
}

bool Parser::isParameter(const std::string& name) const {
    // 判断规则：全大写（可选下划线）= 参数
    // 示例：
    //   period            → true  (参数)
    //   FAST_PERIOD       → true  (参数)
    //   STD_DEV           → true  (参数)
    //   value             → false (字段)
    //   crossover_type    → false (字段)
    //   overbought      → false (字段)
    
    // 检查是否全大写（可选下划线）
    for (char c : name) {
        if (c != '_' && !std::isupper(static_cast<unsigned char>(c))) {
            return false;
        }
    }
    
    // 至少要有一个大写字母（避免纯下划线被识别为参数）
    bool has_upper = false;
    for (char c : name) {
        if (std::isupper(static_cast<unsigned char>(c))) {
            has_upper = true;
            break;
        }
    }
    
    return has_upper;
}

bool Parser::isTimeframe(const std::string& str) const {
    // 判断规则：检查是否为有效的时间框架格式
    // 支持的格式：
    //   - 分钟：1m, 3m, 5m, 15m, 30m
    //   - 小时：1h, 2h, 4h, 6h, 12h
    //   - 天：1d, 3d
    //   - 周：1w
    //   - 月：1M
    
    if (str.empty()) {
        return false;
    }
    
    // 提取数字部分和单位部分
    size_t i = 0;
    while (i < str.length() && std::isdigit(str[i])) {
        i++;
    }
    
    // 必须有数字部分
    if (i == 0) {
        return false;
    }
    
    // 提取单位
    std::string unit = str.substr(i);
    
    // 检查单位是否有效
    if (unit == "m" || unit == "h" || unit == "d" || unit == "w" || unit == "M") {
        return true;
    }
    
    return false;
}

// ============================================================================
// v4.0: 自定义函数解析实现
// ============================================================================

std::shared_ptr<FunctionDefinitionNode> Parser::parseFunctionDefinition() {
    // 函数定义格式：functionName(params): ReturnType { body }
    
    // 期望函数名（标识符）
    if (!check(TokenType::IDENTIFIER)) {
        throw ParserException("Expected function name");
    }
    std::string func_name = current().value;
    advance();
    
    // 期望 (
    expect(TokenType::LPAREN, "Expected '(' after function name");
    
    // 解析参数列表
    auto parameters = parseFunctionParameters();
    
    // 期望 )
    expect(TokenType::RPAREN, "Expected ')' after parameters");
    
    // 可选的返回类型声明
    std::optional<DeclaredType> return_type;
    if (match(TokenType::COLON)) {
        return_type = parseTypeName();
        if (!return_type.has_value()) {
            throw ParserException("Expected type name after ':'");
        }
    }
    
    // 期望 {
    expect(TokenType::LBRACE, "Expected '{' before function body");
    
    // v4.0: 设置当前函数的参数列表（用于参数引用解析）
    current_function_parameters_.clear();
    for (const auto& param : parameters) {
        current_function_parameters_.push_back(param.name);
    }
    
    // 解析函数体
    auto body = parseFunctionBody();
    
    // v4.0: 清空参数列表
    current_function_parameters_.clear();
    
    // 期望 }
    expect(TokenType::RBRACE, "Expected '}' after function body");
    
    // 创建函数定义节点
    return std::make_shared<FunctionDefinitionNode>(
        func_name,
        parameters,
        body,
        return_type
    );
}

std::vector<FunctionParameter> Parser::parseFunctionParameters() {
    std::vector<FunctionParameter> parameters;
    
    // 空参数列表
    if (check(TokenType::RPAREN)) {
        return parameters;
    }
    
    // 解析第一个参数
    do {
        // 参数名
        if (!check(TokenType::IDENTIFIER)) {
            throw ParserException("Expected parameter name");
        }
        std::string param_name = current().value;
        advance();
        
        // 可选的类型声明
        std::optional<DeclaredType> param_type;
        if (match(TokenType::COLON)) {
            param_type = parseTypeName();
            if (!param_type.has_value()) {
                throw ParserException("Expected type name after ':' in parameter");
            }
        }
        
        parameters.push_back(FunctionParameter(param_name, param_type));
        
    } while (match(TokenType::COMMA));
    
    return parameters;
}

std::vector<StatementNodePtr> Parser::parseFunctionBody() {
    std::vector<StatementNodePtr> statements;
    
    while (!check(TokenType::RBRACE) && !check(TokenType::END_OF_FILE)) {
        statements.push_back(parseStatement());
    }
    
    return statements;
}

StatementNodePtr Parser::parseStatement() {
    // if 语句
    if (check(TokenType::IF)) {
        return parseIfStatement();
    }
    
    // return 语句
    if (check(TokenType::RETURN)) {
        return parseReturnStatement();
    }
    
    // 变量声明或赋值
    if (check(TokenType::USER_VAR)) {
        size_t saved_pos = position_;  // 保存 @var 的位置
        std::string var_name = current().value;
        advance();  // 现在 position_ 指向 @var 后的token
        
        // 检查是声明还是赋值
        if (match(TokenType::COLON)) {
            // 变量声明：@var: Type = expr
            // match() 已经 advance() 了，现在 position_ 指向 Type
            // 回退到变量名位置
            position_ = saved_pos;
            return parseVariableDeclaration();
        } else if (check(TokenType::EQ) || check(TokenType::ADD_ASSIGN) || 
                   check(TokenType::SUB_ASSIGN) || check(TokenType::MUL_ASSIGN) ||
                   check(TokenType::DIV_ASSIGN)) {
            // 变量赋值
            return parseVariableAssignment(var_name);
        } else {
            auto [line, col] = getCurrentLineColumn();
            throw ParserException("Expected ':' or '=' after variable name", line, col);
        }
    }
    
    // 表达式语句（暂不支持，可能是错误）
    throw ParserException("Unexpected token in statement: " + current().value);
}

StatementNodePtr Parser::parseVariableDeclaration() {
    // @var: Type = expr 或 @var = expr
    
    // 期望用户变量
    if (!check(TokenType::USER_VAR)) {
        throw ParserException("Expected user variable name (e.g., @myvar)");
    }
    std::string var_name = current().value;
    advance();
    
    // 可选的类型声明
    std::optional<DeclaredType> declared_type;
    if (match(TokenType::COLON)) {
        declared_type = parseTypeName();
        if (!declared_type.has_value()) {
            throw ParserException("Expected type name after ':'");
        }
    }
    
    // 期望 =
    expect(TokenType::EQ, "Expected '=' in variable declaration");
    
    // 解析初始化表达式
    auto initializer = parseArithmetic();
    
    // 期望分号
    expect(TokenType::SEMICOLON, "Expected ';' after variable declaration");
    
    return std::make_shared<VariableDeclarationNode>(var_name, initializer, declared_type);
}

StatementNodePtr Parser::parseVariableAssignment(const std::string& var_name) {
    // @var = expr 或 @var += expr 等
    
    // 获取赋值运算符
    AssignmentOperator op = AssignmentOperator::ASSIGN;
    if (match(TokenType::EQ)) {
        op = AssignmentOperator::ASSIGN;
    } else if (match(TokenType::ADD_ASSIGN)) {
        op = AssignmentOperator::ADD_ASSIGN;
    } else if (match(TokenType::SUB_ASSIGN)) {
        op = AssignmentOperator::SUB_ASSIGN;
    } else if (match(TokenType::MUL_ASSIGN)) {
        op = AssignmentOperator::MUL_ASSIGN;
    } else if (match(TokenType::DIV_ASSIGN)) {
        op = AssignmentOperator::DIV_ASSIGN;
    } else {
        throw ParserException("Expected assignment operator");
    }
    
    // 解析右侧表达式
    auto value_expr = parseArithmetic();
    
    // 期望分号
    expect(TokenType::SEMICOLON, "Expected ';' after assignment");
    
    return std::make_shared<AssignmentStatementNode>(var_name, value_expr, op);
}

// ============================================================================
// v4.1: 解析顶层变量声明（全局变量）
// 格式：@var: Type = expr; 或 @var: Type; 或 @var = expr;
// ============================================================================
std::shared_ptr<VariableDeclarationNode> Parser::parseTopLevelVariableDeclaration() {
    // 期望用户变量
    if (!check(TokenType::USER_VAR)) {
        throw ParserException("Expected user variable name (e.g., @myvar)");
    }
    std::string var_name = current().value;
    advance();
    
    // 期望冒号
    expect(TokenType::COLON, "Expected ':' after variable name in declaration");
    
    // 解析类型名称
    auto declared_type = parseTypeName();
    if (!declared_type.has_value()) {
        throw ParserException("Expected type name after ':'");
    }
    
    // 检查是否有初始化表达式
    ASTNodePtr initializer = nullptr;
    if (match(TokenType::EQ)) {
        // @var: Type = expr;
        initializer = parseArithmetic();
    }
    // else: @var: Type; （只声明不初始化）
    
    return std::make_shared<VariableDeclarationNode>(var_name, initializer, declared_type);
}

// ============================================================================
// v4.1: 解析顶层变量赋值（全局变量）
// 格式：@var = expr; 或 @var += expr; 等
// 注意：var_name 已经被 parseStatements() 消费了
// ============================================================================
std::shared_ptr<AssignmentStatementNode> Parser::parseTopLevelVariableAssignment(const std::string& var_name) {
    // 获取赋值运算符
    AssignmentOperator op = AssignmentOperator::ASSIGN;
    if (match(TokenType::EQ)) {
        op = AssignmentOperator::ASSIGN;
    } else if (match(TokenType::ADD_ASSIGN)) {
        op = AssignmentOperator::ADD_ASSIGN;
    } else if (match(TokenType::SUB_ASSIGN)) {
        op = AssignmentOperator::SUB_ASSIGN;
    } else if (match(TokenType::MUL_ASSIGN)) {
        op = AssignmentOperator::MUL_ASSIGN;
    } else if (match(TokenType::DIV_ASSIGN)) {
        op = AssignmentOperator::DIV_ASSIGN;
    } else {
        throw ParserException("Expected assignment operator after variable name");
    }
    
    // 解析右侧表达式
    auto value_expr = parseArithmetic();
    
    return std::make_shared<AssignmentStatementNode>(var_name, value_expr, op);
}

StatementNodePtr Parser::parseIfStatement() {
    // if (condition) { statements } else { statements }
    
    // 消费 if
    expect(TokenType::IF, "Expected 'if'");
    
    // 期望 (
    expect(TokenType::LPAREN, "Expected '(' after 'if'");
    
    // 解析条件表达式（需要支持比较和逻辑运算）
    auto condition = parseLogicalOr();
    
    // 期望 )
    expect(TokenType::RPAREN, "Expected ')' after condition");
    
    // 期望 {
    expect(TokenType::LBRACE, "Expected '{' before if body");
    
    // 解析 then 分支
    std::vector<StatementNodePtr> then_branch;
    while (!check(TokenType::RBRACE) && !check(TokenType::END_OF_FILE)) {
        then_branch.push_back(parseStatement());
    }
    
    // 期望 }
    expect(TokenType::RBRACE, "Expected '}' after if body");
    
    // 可选的 else 分支
    std::vector<StatementNodePtr> else_branch;
    if (match(TokenType::ELSE)) {
        // 检查是否为 else if
        if (check(TokenType::IF)) {
            // else if - 作为嵌套的 if 语句
            else_branch.push_back(parseIfStatement());
        } else {
            // 单独的 else
            expect(TokenType::LBRACE, "Expected '{' after 'else'");
            
            while (!check(TokenType::RBRACE) && !check(TokenType::END_OF_FILE)) {
                else_branch.push_back(parseStatement());
            }
            
            expect(TokenType::RBRACE, "Expected '}' after else body");
        }
    }
    
    return std::make_shared<IfStatementNode>(condition, then_branch, else_branch);
}

StatementNodePtr Parser::parseReturnStatement() {
    // return expression;
    
    // 消费 return
    expect(TokenType::RETURN, "Expected 'return'");
    
    // 解析返回表达式
    auto return_expr = parseArithmetic();
    
    // 期望分号
    expect(TokenType::SEMICOLON, "Expected ';' after return statement");
    
    return std::make_shared<ReturnStatementNode>(return_expr);
}

ASTNodePtr Parser::parseUserVar() {
    // @myvar
    
    if (!check(TokenType::USER_VAR)) {
        throw ParserException("Expected user variable");
    }
    
    std::string var_name = current().value;
    advance();
    
    return std::make_shared<VariableReferenceNode>(var_name);
}

ASTNodePtr Parser::parseTernaryExpression(ASTNodePtr condition) {
    // condition ? true_expr : false_expr
    
    // 期望 ?
    expect(TokenType::QUESTION, "Expected '?' in ternary expression");
    
    // 解析 true 分支
    auto true_expr = parseArithmetic();
    
    // 期望 :
    expect(TokenType::COLON, "Expected ':' in ternary expression");
    
    // 解析 false 分支
    auto false_expr = parseArithmetic();
    
    return std::make_shared<TernaryExpressionNode>(condition, true_expr, false_expr);
}

ASTNodePtr Parser::parseFunctionCall(const std::string& func_name) {
    // functionName(args)
    
    // 期望 (
    expect(TokenType::LPAREN, "Expected '(' after function name");
    
    // 解析参数列表
    std::vector<ASTNodePtr> arguments;
    
    if (!check(TokenType::RPAREN)) {
        do {
            arguments.push_back(parseArithmetic());
        } while (match(TokenType::COMMA));
    }
    
    // 期望 )
    expect(TokenType::RPAREN, "Expected ')' after arguments");
    
    return std::make_shared<FunctionCallNode>(func_name, arguments);
}

std::optional<DeclaredType> Parser::parseTypeName() {
    if (!check(TokenType::TYPE_NAME)) {
        return std::nullopt;
    }
    
    std::string type_str = current().value;
    advance();
    
    return string_to_declared_type(type_str);
}

ASTNodePtr Parser::parseTimeSeriesFunction() {
    // FUNDINGRATE().method(params)
    // FEARGREED().method(params)
    // CURRENT().price
    // CURRENT().time.hour
    // CURRENT().time(8).hour
    // CURRENT().feargreed.value
    // CURRENT().feargreed(-1).value
    std::string func_name = current().value;
    advance();

    expect(TokenType::LPAREN, "Expected '(' after time series function name");
    expect(TokenType::RPAREN, "Expected ')' after time series function name");
    expect(TokenType::DOT, "Expected '.' after time series function()");

    if (!check(TokenType::IDENTIFIER)) {
        auto [line, col] = getCurrentLineColumn();
        std::string context = position_ < tokens_.size() ? 
            (", got: " + current().value) : ", reached end of input";
        throw ParserException("Expected field name after " + func_name + "()" + context, line, col);
    }

    std::string field_name = current().value;
    advance();

    // 解析字段参数（可以为空）
    // 例如：CURRENT().time(8) 或 CURRENT().feargreed(-1)
    std::vector<ASTNodePtr> field_params;
    
    if (check(TokenType::LPAREN)) {
        advance();  // 消费 '('
        
        if (!check(TokenType::RPAREN)) {
            // 有参数：解析参数列表
            do {
                field_params.push_back(parseArithmetic());
            } while (match(TokenType::COMMA));
        }
        
        expect(TokenType::RPAREN, "Expected ')' after field parameters");
    }
    
    // 检查是否有子字段（如 CURRENT().time.hour 中的 "hour"）
    std::string subfield = "";
    std::vector<ASTNodePtr> submethod_params;
    
    if (check(TokenType::DOT)) {
        advance();  // 消费 '.'
        
        if (!check(TokenType::IDENTIFIER)) {
            auto [line, col] = getCurrentLineColumn();
            throw ParserException("Expected subfield name after " + func_name + "()." + field_name + ".", line, col);
        }
        
        subfield = current().value;
        advance();
        
        // 检查子字段后面是否有括号（如 SWING(5m).high().value(0)）
        if (check(TokenType::LPAREN)) {
            advance();  // 消费 '('
            
            if (!check(TokenType::RPAREN)) {
                // 有参数：解析参数列表
                do {
                    submethod_params.push_back(parseArithmetic());
                } while (match(TokenType::COMMA));
            }
            
            expect(TokenType::RPAREN, "Expected ')' after submethod parameters");
        }
    }
    
    // 时间序列函数使用 IndicatorRefNode，timeframe 为空字符串
    // field_name: 第一级字段（time, feargreed, price等）
    // field_params: 第一级字段的参数（如 time(8) 中的 8）
    // subfield: 第二级字段（如 time.hour 中的 hour）
    // submethod_params: 第二级字段的参数（如果有的话）
    return std::make_shared<IndicatorRefNode>(func_name, "", field_name, field_params, subfield, std::vector<Value>(), submethod_params);
}

bool Parser::isTimeSeriesFunction(const std::string& name) const {
    // 时间序列函数列表：CURRENT, FUNDINGRATE, FEARGREED等
    // 后续添加新的时间序列函数时，只需在此处添加即可
    return name == "CURRENT" || name == "FUNDINGRATE" || name == "FEARGREED";
}

bool Parser::isFunctionName(const std::string& name) const {
    return function_registry_.has_function(name);
}

} // namespace prophet::dsl
