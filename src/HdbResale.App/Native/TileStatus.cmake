# SPDX-License-Identifier: GPL-3.0-or-later
# Included through the pinned Bridge's QtDotnetCMake extension item.
get_filename_component(HDB_TILE_STATUS_SOURCE_DIR "${CMAKE_CURRENT_LIST_FILE}" DIRECTORY)
find_package(Qt6 REQUIRED COMPONENTS Core)
add_library(hdb_tile_status SHARED "${HDB_TILE_STATUS_SOURCE_DIR}/tile_status.cpp")
target_compile_features(hdb_tile_status PRIVATE cxx_std_17)
target_compile_definitions(hdb_tile_status PRIVATE HDB_TILE_STATUS_BUILD)
target_link_libraries(hdb_tile_status PRIVATE Qt6::Core)
set_target_properties(hdb_tile_status PROPERTIES CXX_VISIBILITY_PRESET hidden)
install(TARGETS hdb_tile_status LIBRARY DESTINATION . RUNTIME DESTINATION .)

enable_testing()
add_executable(hdb_tile_status_tests "${HDB_TILE_STATUS_SOURCE_DIR}/tile_status_tests.cpp")
target_compile_features(hdb_tile_status_tests PRIVATE cxx_std_17)
target_link_libraries(hdb_tile_status_tests PRIVATE hdb_tile_status Qt6::Core)
add_test(NAME hdb_tile_status COMMAND hdb_tile_status_tests)
