/*
 * ============================================================================
 * 文件名：lexer.hpp
 * 功能说明：词法分析器头文件
 * 
 * 定义了词法分析器Lexer类和Token结构
 * 
 * Lexer的作用：
 * - 将DSL源代码字符串分解成Token（词法单元）序列
 * - 识别关键字、标识符、运算符、数字、字符串等
 * - 跳过空白字符和注释
 * - 为Parser（语法分析器）准备输入
 * 
 * Token类型（定义在types.hpp中的TokenType）：
 * - 关键字：ALL、ANY、BUY、SELL、HOLD等
 * - 运算符：+、-、*、/、>、<、==、!=等
 * - 字面量：NUMBER、STRING、BOOLEAN
 * - 标识符：IDENTIFIER（指标名、变量名等）
 * - 分隔符：(、)、{、}、[、]、,、;等
 * ============================================================================
 */

#pragma once

#include "../common/types.hpp"
#include <string>
#include <vector>

namespace prophet::dsl {

/**
 * Token 结构
 * 表示源代码中的一个词法单元
 */
struct Token {
    TokenType type;
    std::string value;
    size_t position;  // 在源码中的位置

    Token(TokenType t, const std::string& v = "", size_t pos = 0)
        : type(t), value(v), position(pos) {}
};

/**
 * 词法分析器
 *
 * 将 DSL 字符串分解为 Token 流
 */
class Lexer {
public:
    explicit Lexer(const std::string& source);

    // 解析整个源码，返回 Token 列表
    std::vector<Token> tokenize();

private:
    std::string source_;
    size_t position_;
    size_t length_;

    // 当前字符
    char current() const;

    // 前进一个字符
    void advance();

    // 跳过空白字符
    void skipWhitespace();

    // 跳过单行注释 // ...
    void skipSingleLineComment();

    // 跳过多行注释 /* ... */
    void skipMultiLineComment();

    // 跳过空白字符和注释（统一入口）
    void skipWhitespaceAndComments();

    // 读取数字
    Token readNumber();

    // 读取时间框架或数字（5m, 1h, 123, 45.67）
    Token readTimeframeOrNumber();

    // 读取标识符或关键字
    Token readIdentifier();

    // 读取参数引用 $INDICATOR(TF).PARAM
    Token readParamRef();

    // 读取用户变量 @variable_name
    Token readUserVariable();

    // 读取字符串（如果支持）
    Token readString();

    // 检查是否为关键字
    TokenType checkKeyword(const std::string& identifier);
    
    // 计算位置对应的行号和列号（用于错误报告）
    std::pair<int, int> getLineColumn(size_t pos) const;
};

} // namespace prophet::dsl
