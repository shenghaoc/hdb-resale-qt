# SPDX-License-Identifier: GPL-3.0-or-later
# Included through the pinned Bridge's QtDotnetCMake extension item.
get_filename_component(HDB_TILE_STATUS_SOURCE_DIR "${CMAKE_CURRENT_LIST_FILE}" DIRECTORY)
find_package(Qt6 REQUIRED COMPONENTS Core)
add_library(hdb_tile_status SHARED "${HDB_TILE_STATUS_SOURCE_DIR}/tile_status.cpp")
target_compile_features(hdb_tile_status PRIVATE cxx_std_17)
target_compile_definitions(hdb_tile_status PRIVATE HDB_TILE_STATUS_BUILD)
target_link_libraries(hdb_tile_status PRIVATE Qt6::Core)
set_target_properties(hdb_tile_status PROPERTIES CXX_VISIBILITY_PRESET hidden)
# CMake edits the installed library's RPATH after copying it, and the Bridge reinstalls on every build.
# An unchanged library is skipped as up to date, so remove the previous copy first; otherwise the edit is
# applied twice and fails, breaking incremental builds.
install(CODE "file(REMOVE \"\${CMAKE_INSTALL_PREFIX}/$<TARGET_FILE_NAME:hdb_tile_status>\")")
install(TARGETS hdb_tile_status LIBRARY DESTINATION . RUNTIME DESTINATION .)

enable_testing()
add_executable(hdb_tile_status_tests "${HDB_TILE_STATUS_SOURCE_DIR}/tile_status_tests.cpp")
target_compile_features(hdb_tile_status_tests PRIVATE cxx_std_17)
target_link_libraries(hdb_tile_status_tests PRIVATE hdb_tile_status Qt6::Core)
add_test(NAME hdb_tile_status COMMAND hdb_tile_status_tests)

add_executable(hdb_tile_status_handoff_tests "${HDB_TILE_STATUS_SOURCE_DIR}/tile_status_handoff_tests.cpp")
target_compile_features(hdb_tile_status_handoff_tests PRIVATE cxx_std_17)
target_link_libraries(hdb_tile_status_handoff_tests PRIVATE hdb_tile_status Qt6::Core)
add_test(NAME hdb_tile_status_handoff COMMAND hdb_tile_status_handoff_tests)

add_executable(hdb_tile_status_default_sink_tests "${HDB_TILE_STATUS_SOURCE_DIR}/tile_status_default_sink_tests.cpp")
target_compile_features(hdb_tile_status_default_sink_tests PRIVATE cxx_std_17)
target_link_libraries(hdb_tile_status_default_sink_tests PRIVATE hdb_tile_status Qt6::Core)
add_test(NAME hdb_tile_status_default_sink COMMAND hdb_tile_status_default_sink_tests)
# The pass expression replaces the exit status, so internal failures print a marker instead.
set_tests_properties(hdb_tile_status_default_sink PROPERTIES
    ENVIRONMENT "QT_FORCE_STDERR_LOGGING=1"
    PASS_REGULAR_EXPRESSION "HDB_DEFAULT_SINK_BEFORE.*giving up.*HDB_DEFAULT_SINK_AFTER_STOP"
    FAIL_REGULAR_EXPRESSION "HDB_TEST_FAILED")
