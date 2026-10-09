# SPDX-License-Identifier: GPL-3.0-or-later
cmake_minimum_required(VERSION 3.22)
project(KDEUpstreamInputSynth LANGUAGES C CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_AUTOMOC ON)
if(NOT KDE_SOURCE OR NOT FAKE_INPUT_XML)
    message(FATAL_ERROR "Pass the pinned upstream KDE_SOURCE and FAKE_INPUT_XML paths")
endif()
find_package(Qt6 REQUIRED COMPONENTS Core Gui DBus WaylandClient)
find_package(PkgConfig REQUIRED)
pkg_check_modules(xkbcommon xkbcommon REQUIRED IMPORTED_TARGET)
pkg_check_modules(wayland-client wayland-client REQUIRED IMPORTED_TARGET)
add_executable(selenium-webdriver-at-spi-inputsynth
    "${KDE_SOURCE}/inputsynth/main.cpp" "${KDE_SOURCE}/inputsynth/interaction.cpp")
qt6_generate_wayland_protocol_client_sources(selenium-webdriver-at-spi-inputsynth
    FILES "${FAKE_INPUT_XML}")
target_link_libraries(selenium-webdriver-at-spi-inputsynth PRIVATE
    Qt6::Core Qt6::Gui Qt6::DBus Qt6::WaylandClient
    PkgConfig::wayland-client PkgConfig::xkbcommon)
