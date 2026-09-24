/*
 * ============================================================================
 * 文件名：lexer.cpp
 * 功能说明：词法分析器（Lexer）实现
 * 
 * 什么是词法分析？
 *   把一段文本（代码）拆分成一个个"单词"（Token）的过程
 *   就像阅读文章时，先要识别每个字、词一样
 * 
 * 举个例子：
 *   输入文本："MACD > 0 AND RSI < 70"
 *   词法分析后得到的Token列表：
 *     ["MACD", ">", "0", "AND", "RSI", "<", "70"]
 * 
 * 主要功能：
 * 1. 扫描源代码字符串
 * 2. 识别关键字（如ALL、ANY、BUY、SELL等）
 * 3. 识别标识符（如MACD、RSI等指标名）
 * 4. 识别运算符（如>、<、=等）
 * 5. 识别字面量（如数字、字符串）
 * 6. 跳过空白和注释
 * 
 * 为什么需要词法分析？
 * - 把连续的字符串变成有意义的"词汇单元"
 * - 为后续的语法分析（Parser）做准备
 * - 过滤掉无用的空白和注释
 * 
 * 简单理解：
 * - 词法分析器就像一个"文字识别器"
 * - 它把一串字符变成一个个有意义的"单词"
 * ============================================================================
 */

#include "prophet/dsl/lexer.hpp"
#include <cctype>
#include <unordered_map>

namespace prophet::dsl {

// ============================================================================
// 构造函数 - 创建词法分析器
// 参数：
//   source - 要分析的源代码字符串
// ============================================================================
Lexer::Lexer(const std::string& source)
    : source_(source)                  // 保存源代码
    , position_(0)                     // 当前读取位置从0开始
    , length_(source.length())         // 记录源代码长度
{}

// ========== 基础辅助函数 ==========

// 获取当前位置的字符
char Lexer::current() const {
    if (position_ >= length_) {
        return '\0';  // 已到达字符串末尾，返回空字符
    }
    return source_[position_];
}

// 前进一个字符（移动读取位置）
void Lexer::advance() {
    if (position_ < length_) {
        position_++;
    }
}

// 跳过空白字符（空格、换行、制表符等）
void Lexer::skipWhitespace() {
    while (position_ < length_ && std::isspace(current())) {
        advance();
    }
}

// ============================================================================
// 函数：skipSingleLineComment
// 功能：跳过单行注释
// 
// 单行注释格式：// 注释内容
// 从 // 开始到行尾都是注释
// ============================================================================
void Lexer::skipSingleLineComment() {
    // 跳过开头的两个斜杠 //
    advance();  // 跳过第一个 /
    advance();  // 跳过第二个 /
    
    // 继续读取直到遇到换行符或文件结束
    while (current() != '\n' && current() != '\0') {
        advance();
    }
    
    // 如果是换行符，也跳过它
    if (current() == '\n') {
        advance();
    }
}

// ============================================================================
// 函数：skipMultiLineComment
// 功能：跳过多行注释
// 
// 多行注释格式：/* 注释内容 */
// 可以跨越多行
// ============================================================================
void Lexer::skipMultiLineComment() {
    size_t comment_start = position_;  // 记录注释开始位置，用于错误提示
    
    // 跳过开头的 /*
    advance();  // 跳过 /
    advance();  // 跳过 *
    
    // 继续读取直到找到 */ 或文件结束
    while (position_ < length_) {
        // 检查是否遇到注释结束标记 */
        if (current() == '*' && position_ + 1 < length_ && source_[position_ + 1] == '/') {
            advance();  // 跳过 *
            advance();  // 跳过 /
            return;     // 注释结束，返回
        }
        advance();
    }
    
    // 如果到达文件结束还没找到 */，说明注释没有正确结束
    auto [line, col] = getLineColumn(comment_start);
    throw LexerException("Unterminated multi-line comment", line, col);
}

void Lexer::skipWhitespaceAndComments() {
    while (position_ < length_) {
        char ch = current();
        
        // 跳过空白字符
        if (std::isspace(ch)) {
            advance();
            continue;
        }
        
        // 检查注释
        if (ch == '/') {
            // 需要向前看一个字符
            if (position_ + 1 < length_) {
                char next = source_[position_ + 1];
                
                if (next == '/') {
                    // 单行注释
                    skipSingleLineComment();
                    continue;
                } else if (next == '*') {
                    // 多行注释
                    skipMultiLineComment();
                    continue;
                }
            }
            // 如果不是注释，则是除法运算符，退出循环
            break;
        }
        
        // 既不是空白也不是注释，退出循环
        break;
    }
}

// ============================================================================
// 函数：readNumber
// 功能：读取数字
// 
// 支持的格式：
//   - 整数：123
//   - 小数：123.45
// 
// 读取过程：
//   1. 先读取整数部分（连续的数字）
//   2. 如果遇到小数点，继续读取小数部分
//   3. 返回NUMBER类型的Token
// ============================================================================
Token Lexer::readNumber() {
    size_t start = position_;  // 记录数字开始位置
    std::string num;

    // 第1步：读取整数部分
    // 例如：123 或 123.45 中的 123
    while (std::isdigit(current())) {
        num += current();
        advance();
    }

    // 第2步：检查是否有小数部分
    if (current() == '.') {
        num += current();  // 添加小数点
        advance();
        
        // 读取小数部分
        // 例如：123.45 中的 45
        while (std::isdigit(current())) {
            num += current();
            advance();
        }
    }

    return Token(TokenType::NUMBER, num, start);
}

// ============================================================================
// 函数：readIdentifier
// 功能：读取标识符（变量名、函数名、关键字等）
// 
// 标识符规则：
//   - 可以包含字母、数字、下划线
//   - 通常以字母或下划线开头
//   - 例如：MACD、RSI、my_var、_temp
// 
// 读取后会检查是否为关键字：
//   - 如果是关键字（如ALL、BUY），返回对应的关键字类型
//   - 否则返回IDENTIFIER类型
// ============================================================================
Token Lexer::readIdentifier() {
    size_t start = position_;  // 记录标识符开始位置
    std::string ident;

    // 读取标识符的所有字符
    // 允许字母、数字、下划线
    while (std::isalnum(current()) || current() == '_') {
        ident += current();
        advance();
    }

    // 检查这个标识符是否为关键字（如ALL、ANY、BUY等）
    TokenType type = checkKeyword(ident);

    return Token(type, ident, start);
}

// 读取时间框架（可能以数字开头，如 5m, 1h）
Token Lexer::readTimeframeOrNumber() {
    size_t start = position_;
    std::string value;

    // 读取数字部分
    while (std::isdigit(current())) {
        value += current();
        advance();
    }

    // 如果后面跟着字母（时间框架单位），继续读取
    if (std::isalpha(current())) {
        while (std::isalnum(current()) || current() == '_') {
            value += current();
            advance();
        }
        // 这是时间框架标识符
        return Token(TokenType::IDENTIFIER, value, start);
    }

    // 否则检查小数点
    if (current() == '.') {
        value += current();
        advance();
        while (std::isdigit(current())) {
            value += current();
            advance();
        }
    }

    return Token(TokenType::NUMBER, value, start);
}

// readParamRef() 已废弃 - 不再支持 #. 语法
// 现在统一使用 $(tf).INDICATOR().xxx，在 Parser 层通过命名约定区分参数和字段
Token Lexer::readParamRef() {
    // 此函数已废弃，保留空实现以防编译错误
    auto [line, col] = getLineColumn(position_);
    throw LexerException("readParamRef() is deprecated. Use $(tf).INDICATOR().xxx syntax instead", line, col);
}

// ============================================================================
// 函数：readUserVariable
// 功能：读取用户变量（以 @ 开头）
// 
// 格式：@variable_name
// 命名规范：
//   - 用户变量：全小写 + 下划线（如 @score, @my_var, @rsi_value）
//   - 旧的环境变量已废弃，现在所有以 @ 开头的都是用户变量
// ============================================================================
Token Lexer::readUserVariable() {
    size_t start = position_;  // 记录起始位置
    std::string var_name;
    
    // 跳过 @
    advance();
    
    // 读取变量名（字母、数字、下划线）
    // 第一个字符必须是字母或下划线
    if (!std::isalpha(current()) && current() != '_') {
        auto [line, col] = getLineColumn(position_);
        throw LexerException("Invalid variable name: variable name must start with a letter or underscore after '@'", line, col);
    }
    
    // 读取完整的变量名
    while (std::isalnum(current()) || current() == '_') {
        var_name += current();
        advance();
    }
    
    // 所有以 @ 开头的变量都作为 USER_VAR token 返回
    // 不再区分环境变量和用户变量
    return Token(TokenType::USER_VAR, var_name, start);
}

Token Lexer::readString() {
    size_t start = position_;
    char quote = current(); // ' 或 "
    advance(); // 跳过开始引号

    std::string str;
    while (current() != quote && current() != '\0') {
        if (current() == '\\') {
            advance();
            // 转义字符
            switch (current()) {
                case 'n':  str += '\n'; break;
                case 't':  str += '\t'; break;
                case '\\': str += '\\'; break;
                case '"':  str += '"'; break;
                case '\'': str += '\''; break;
                default:   str += current(); break;
            }
        } else {
            str += current();
        }
        advance();
    }

    if (current() != quote) {
        auto [line, col] = getLineColumn(start);
        throw LexerException("Unterminated string", line, col);
    }

    advance(); // 跳过结束引号
    return Token(TokenType::STRING, str, start);
}

// ============================================================================
// 函数：checkKeyword
// 功能：检查一个标识符是否为关键字
// 
// 关键字分类：
//   1. 信号函数：ALL、ANY、NONE、MIN、COUNT、MAX、WEIGHTED
//   2. 数据函数：CONSECUTIVE、CONSEC
//   3. 逻辑运算：WEIGHT、NOT、AND、OR
//   4. 控制流：if、else、return
//   5. 信号类型：BUY、SELL、HOLD
//   6. 范围运算：BETWEEN、IN、NEAR
//   7. 布尔值：true、false
//   8. 类型名：Integer、Double、Boolean、String、Enum
// 
// 返回值：
//   - 如果是关键字，返回对应的TokenType
//   - 否则返回IDENTIFIER
// ============================================================================
TokenType Lexer::checkKeyword(const std::string& identifier) {
    // 定义关键字映射表（static表示只初始化一次，提高性能）
    static const std::unordered_map<std::string, TokenType> keywords = {
        // === 信号函数关键字 ===
        {"ALL", TokenType::ALL},           // 所有条件都满足
        {"ANY", TokenType::ANY},           // 任一条件满足
        {"NONE", TokenType::NONE},         // 所有条件都不满足
        {"MIN", TokenType::MIN},           // 最少满足N个条件
        {"COUNT", TokenType::COUNT},       // 恰好满足N个条件
        {"MAX", TokenType::MAX},           // 最多满足N个条件
        {"VOTE", TokenType::VOTE},         // 投票机制（至少N个或百分比）
        {"WEIGHTED", TokenType::WEIGHTED}, // 加权条件组合

        // === CONSECUTIVE和CONSEC已重构为普通数据函数，不再是特殊关键字 ===

        // === 逻辑运算关键字 ===
        {"WEIGHT", TokenType::WEIGHT},     // 权重定义
        {"NOT", TokenType::NOT},           // 逻辑非
        {"AND", TokenType::AND},           // 逻辑与
        {"OR", TokenType::OR},             // 逻辑或

        // === 控制流关键字 ===
        {"if", TokenType::IF},             // 条件判断
        {"else", TokenType::ELSE},         // 否则分支
        {"return", TokenType::RETURN},     // 返回语句

        // === 信号类型关键字 ===
        {"BUY", TokenType::BUY},           // 买入信号
        {"SELL", TokenType::SELL},         // 卖出信号
        {"HOLD", TokenType::HOLD},         // 持有信号

        // === 范围运算关键字 ===
        {"BETWEEN", TokenType::BETWEEN},   // 在...之间
        {"IN", TokenType::IN},             // 在...之中
        {"NEAR", TokenType::NEAR},         // 接近...

        // === 布尔值字面量 ===
        {"true", TokenType::BOOLEAN},      // 真
        {"false", TokenType::BOOLEAN},     // 假
        
        // === 类型名称 ===
        {"Integer", TokenType::TYPE_NAME}, // 整数类型
        {"Double", TokenType::TYPE_NAME},  // 浮点数类型
        {"Boolean", TokenType::TYPE_NAME}, // 布尔类型
        {"String", TokenType::TYPE_NAME},  // 字符串类型
        {"Enum", TokenType::TYPE_NAME},    // 枚举类型
        {"Void", TokenType::TYPE_NAME},    // 无返回值类型
    };

    // 在关键字表中查找
    auto it = keywords.find(identifier);
    if (it != keywords.end()) {
        return it->second;  // 找到了，返回对应的TokenType
    }

    return TokenType::IDENTIFIER;  // 不是关键字，返回普通标识符类型
}

// ============================================================================
// 函数：tokenize
// 功能：执行词法分析，将源代码转换为Token序列
// 
// 这是词法分析器的主入口函数！
// 
// 工作流程：
//   1. 从头到尾扫描源代码字符串
//   2. 识别每个"单词"（Token）
//   3. 跳过空白字符和注释
//   4. 返回Token数组
// 
// 返回值：
//   Token数组，每个Token包含：
//     - type：Token类型（NUMBER、IDENTIFIER、关键字等）
//     - value：Token的文本内容
//     - position：在源代码中的位置
// 
// 例子：
//   输入："$(5m).MACD().value > 0"
//   输出：[PARAM_REF, DOT, IDENTIFIER(MACD), LPAREN, IDENTIFIER(5m), RPAREN, 
//          DOT, IDENTIFIER(value), GT, NUMBER(0), END_OF_FILE]
// ============================================================================
std::vector<Token> Lexer::tokenize() {
    std::vector<Token> tokens;  // 存储识别出的所有Token

    while (position_ < length_) {
        // 第1步：跳过空白字符和注释
        // 这些对程序逻辑没有影响，可以忽略
        skipWhitespaceAndComments();

        if (position_ >= length_) {
            break;  // 已到达文件末尾
        }

        char ch = current();  // 获取当前字符

        // ========== 第2步：根据当前字符判断Token类型 ==========
        
        // 情况1：数字或时间框架（如5m、1h、123、45.67）
        if (std::isdigit(ch)) {
            tokens.push_back(readTimeframeOrNumber());
            continue;
        }

        // 情况2：标识符或关键字（如MACD、RSI、ALL、BUY）
        if (std::isalpha(ch) || ch == '_') {
            tokens.push_back(readIdentifier());
            continue;
        }

        // 情况3：指标引用前缀 $(timeframe).INDICATOR(params).field (新语法)
        if (ch == '$') {
            size_t start = position_;
            // 检查是否为新语法 $(
            if (position_ + 1 < length_ && source_[position_ + 1] == '(') {
                // 新语法：$(timeframe).INDICATOR(params).field
                advance(); // 跳过 $
                // 返回一个特殊的 INDICATOR_REF_START token，表示新语法开始
                tokens.push_back(Token(TokenType::INDICATOR_REF_START, "$", start));
                // 注意：'(' 会在后续处理中被识别为 LPAREN
            } else {
                // $ 后面必须跟 ( 才是合法的指标引用
                auto [line, col] = getLineColumn(position_);
                throw LexerException("Invalid '$' character. Use '$(timeframe).INDICATOR(params).field' for indicator reference (e.g., $(5m).MACD(12,26,9).trend)", line, col);
            }
            continue;
        }

        // 情况4：用户变量 @variable_name（如 @score, @my_var）
        if (ch == '@') {
            tokens.push_back(readUserVariable());
            continue;
        }

        // 旧语法已废弃：不再支持 #. 参数引用

        // 情况5：字符串（如"BULLISH"、'mystring'）
        if (ch == '"' || ch == '\'') {
            tokens.push_back(readString());
            continue;
        }

        // ========== 第3步：处理运算符和分隔符 ==========
        size_t start = position_;  // 记录起始位置

        switch (ch) {  // 根据当前字符判断是哪种运算符
            case '=':  // 等号：= 或 ==
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::EQ, "==", start));  // ==（相等比较）
                } else {
                    tokens.push_back(Token(TokenType::EQ, "=", start));   // =（赋值或比较）
                }
                break;

            case '!':  // 感叹号：只支持 !=
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::NE, "!=", start));  // !=（不等于）
                } else {
                    // 单独的 ! 不支持，报错
                    auto [line, col] = getLineColumn(start);
                    throw LexerException("Unexpected character '!'", line, col);
                }
                break;

            case '>':  // 大于号：> 或 >=
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::GE, ">=", start));  // >=（大于等于）
                } else {
                    tokens.push_back(Token(TokenType::GT, ">", start));   // >（大于）
                }
                break;

            case '<':  // 小于号：< 或 <=
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::LE, "<=", start));  // <=（小于等于）
                } else {
                    tokens.push_back(Token(TokenType::LT, "<", start));   // <（小于）
                }
                break;

            case '+':
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::ADD_ASSIGN, "+=", start));
                } else {
                    tokens.push_back(Token(TokenType::PLUS, "+", start));
                }
                break;

            case '-':
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::SUB_ASSIGN, "-=", start));
                } else {
                    tokens.push_back(Token(TokenType::MINUS, "-", start));
                }
                break;

            case '*':
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::MUL_ASSIGN, "*=", start));
                } else {
                    tokens.push_back(Token(TokenType::MULTIPLY, "*", start));
                }
                break;

            case '/':
                // 注释已经在 skipWhitespaceAndComments() 中处理
                // 到这里说明是除法运算符
                advance();
                if (current() == '=') {
                    advance();
                    tokens.push_back(Token(TokenType::DIV_ASSIGN, "/=", start));
                } else {
                    tokens.push_back(Token(TokenType::DIVIDE, "/", start));
                }
                break;

            case '%':
                tokens.push_back(Token(TokenType::MODULO, "%", start));
                advance();
                break;

            case '(':
                tokens.push_back(Token(TokenType::LPAREN, "(", start));
                advance();
                break;

            case ')':
                tokens.push_back(Token(TokenType::RPAREN, ")", start));
                advance();
                break;

            case '{':
                tokens.push_back(Token(TokenType::LBRACE, "{", start));
                advance();
                break;

            case '}':
                tokens.push_back(Token(TokenType::RBRACE, "}", start));
                advance();
                break;

            case '[':
                tokens.push_back(Token(TokenType::LBRACKET, "[", start));
                advance();
                break;

            case ']':
                tokens.push_back(Token(TokenType::RBRACKET, "]", start));
                advance();
                break;

            case ',':
                tokens.push_back(Token(TokenType::COMMA, ",", start));
                advance();
                break;

            case '.':
                tokens.push_back(Token(TokenType::DOT, ".", start));
                advance();
                break;

            case ';':
                tokens.push_back(Token(TokenType::SEMICOLON, ";", start));
                advance();
                break;

            case '?':
                tokens.push_back(Token(TokenType::QUESTION, "?", start));
                advance();
                break;

            case ':':
                tokens.push_back(Token(TokenType::COLON, ":", start));
                advance();
                break;

            default:
                // 遇到未知字符，抛出异常
                auto [line, col] = getLineColumn(start);
                throw LexerException("Unexpected character '" + std::string(1, ch) + "'", line, col);
        }
    }

    // ========== 第4步：添加文件结束标记 ==========
    // 在Token序列末尾添加END_OF_FILE标记
    // 这样Parser就知道已经到达文件末尾了
    tokens.push_back(Token(TokenType::END_OF_FILE, "", position_));
    
    return tokens;
}

// ============================================================================
// 函数：getLineColumn
// 功能：计算位置对应的行号和列号（用于错误报告）
// ============================================================================
std::pair<int, int> Lexer::getLineColumn(size_t pos) const {
    int line = 1;
    int column = 1;
    
    for (size_t i = 0; i < pos && i < length_; i++) {
        if (source_[i] == '\n') {
            line++;
            column = 1;
        } else {
            column++;
        }
    }
    
    return std::make_pair(line, column);
}

// ============================================================================
// 文件结束
// ============================================================================

} // namespace prophet::dsl
