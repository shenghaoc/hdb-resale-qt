cmake_minimum_required(VERSION 3.21)
project(MapLibreQtLocationSpike LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 17)
find_package(Qt6 6.12.0 EXACT REQUIRED COMPONENTS Core Gui Qml Quick Location Positioning)
qt_add_executable(maplibre-spike main.cpp)
target_link_libraries(maplibre-spike PRIVATE Qt6::Core Qt6::Gui Qt6::Qml Qt6::Quick Qt6::Location Qt6::Positioning)
# Deliberately no required QMapLibre dependency: the probe explains plugin load
# failures; the experiment can be run against a separately installed matching build.
