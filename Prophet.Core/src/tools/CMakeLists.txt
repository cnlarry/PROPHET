set(TOOLS_SOURCES
    kline_converter.cpp
)

add_library(prophet_tools STATIC ${TOOLS_SOURCES})

target_link_libraries(prophet_tools PUBLIC
    ${TALIB_LIBRARIES}
)

target_include_directories(prophet_tools PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/..
    ${CMAKE_CURRENT_SOURCE_DIR}/../include
    ${CMAKE_SOURCE_DIR}/include
)

