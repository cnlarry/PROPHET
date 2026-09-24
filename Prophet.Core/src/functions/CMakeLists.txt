set(FUNCTION_SOURCES
    Frame.cpp
    Weight.cpp
    Logic.cpp
    Data.cpp
    Math.cpp
    TimeSeries.cpp
    Registry.cpp
    Common.cpp
    Base.cpp
    Pattern.cpp
    Sequence.cpp
    FVG.cpp
    PremiumDiscount.cpp
    OrderBlock.cpp
    SwingPoint.cpp
    MarketStructure.cpp
    Liquidity.cpp
    BreakerBlock.cpp
)

add_library(prophet_functions STATIC ${FUNCTION_SOURCES})

target_link_libraries(prophet_functions PUBLIC
    ${TALIB_LIBRARIES}
)

target_include_directories(prophet_functions PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/..
    ${CMAKE_CURRENT_SOURCE_DIR}/../include
    ${CMAKE_SOURCE_DIR}/include
    ${TALIB_INCLUDE_DIR}
)
