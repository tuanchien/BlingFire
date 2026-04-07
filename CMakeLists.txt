cmake_minimum_required (VERSION 3.21)

project (BLING_FIRE)

set (BLING_FIRE_VERSION "0.1.8")

include_directories(${CMAKE_CURRENT_SOURCE_DIR}/blingfireclient.library/inc)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/blingfireclient.library/src)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/blingfirecompile.library/inc)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/blingfirecompile.library/src)

IF (ANDROID)
  IF (ANDROID_PLATFORM MATCHES "^[^0-9]*([0-9]+)$")
    set(_bling_android_api ${CMAKE_MATCH_1})
  ELSE()
    set(_bling_android_api ${CMAKE_SYSTEM_VERSION})
  ENDIF()
  IF (_bling_android_api LESS 28)
    message(FATAL_ERROR "BlingFire requires Android API level 28 or higher (iconv in bionic libc requires API 28+). Pass -DANDROID_PLATFORM=android-28 or higher.")
  ENDIF()
  unset(_bling_android_api)
  # Required for compatibility with devices using 16KB memory pages (Android 15+).
  # This flag is backward-compatible with 4KB page-size devices.
  add_link_options("-Wl,-z,max-page-size=16384")
ENDIF()

IF (APPLE OR CMAKE_SYSTEM_NAME STREQUAL "Emscripten" OR ANDROID)
  set (OPTIMIZE "-O3")
ELSE()
  set (OPTIMIZE "-O4 -fexpensive-optimizations")
ENDIF()

IF (WIN32 AND MSVC)
  set (CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -DNDEBUG")
  add_compile_options("/O2" "/W4" "/GS" "/Gy" "/guard:cf" "/Gm-" "/Zc:inline" "/fp:precise" "/GF" "/EHsc" "/ZH:SHA_256")
  add_compile_options("$<$<CONFIG:Debug>:/Od>")
  add_compile_options("$<$<NOT:$<CONFIG:Debug>>:/GL>")
  add_compile_options("$<$<NOT:$<CONFIG:Debug>>:/Ob2>")
  add_link_options("/guard:cf")
  add_link_options("$<$<NOT:$<CONFIG:Debug>>:/LTCG>")
  set (default_build_type "RelWithDebInfo")
ELSE()
  set (CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -fpic ${OPTIMIZE} -DNDEBUG")
  IF(APPLE)
    set (CMAKE_CXX_FLAGS_RELEASE " -std=c++11 -fpic ${OPTIMIZE} -DNDEBUG -ffunction-sections -fdata-sections -Wl,-dead_strip")
  ELSE()
    set (CMAKE_CXX_FLAGS_RELEASE " -std=c++11 -fpic ${OPTIMIZE} -DNDEBUG -ffunction-sections -fdata-sections -Wl,--gc-sections")
  ENDIF()
  set (CMAKE_CXX_FLAGS_DEBUG " -std=c++11 -fpic -O0 -g")
  set (default_build_type "Release")
ENDIF()

IF (UNIX OR LINUX OR CMAKE_SYSTEM_NAME STREQUAL "Emscripten")
  add_definitions ("-Wall -Wno-unknown-pragmas -DHAVE_ICONV_LIB -DHAVE_NO_SPECSTRINGS -D_VERBOSE -DBLING_FIRE_NOAP -DBLING_FIRE_NOWINDOWS")
  IF (IOS)
    add_definitions (-DBLING_FIRE_IOS)
  ELSEIF (APPLE)
    add_definitions (-DBLING_FIRE_MAC)
  ELSEIF (ANDROID)
    add_definitions (-DBLING_FIRE_ANDROID)
  ENDIF()
ELSE()
  add_definitions ("-DHAVE_NO_SPECSTRINGS -D_VERBOSE -DBLING_FIRE_NOAP")
ENDIF()

# define headers and sources
file(GLOB CLIENT_HEADER_FILES "${CMAKE_CURRENT_SOURCE_DIR}/blingfireclient.library/inc/*.h")
file(GLOB CLIENT_SOURCE_FILES "${CMAKE_CURRENT_SOURCE_DIR}/blingfireclient.library/src/*.cpp")
file(GLOB CLIENT_RESOURCE_FILES "${CMAKE_CURRENT_SOURCE_DIR}/blingfireclient.library/src/*.cxx")

file(GLOB COMPILE_HEADER_FILES "${CMAKE_CURRENT_SOURCE_DIR}/blingfirecompile.library/inc/*.h")
file(GLOB COMPILE_SOURCE_FILES "${CMAKE_CURRENT_SOURCE_DIR}/blingfirecompile.library/src/*.cpp")
file(GLOB COMPILE_RESOURCE_FILES "${CMAKE_CURRENT_SOURCE_DIR}/blingfirecompile.library/src/*.cxx")

# build blingfire client
add_library(fsaClient STATIC ${CLIENT_HEADER_FILES} ${CLIENT_SOURCE_FILES} ${CLIENT_RESOURCE_FILES})

add_library(fsaClientTiny STATIC ${CLIENT_HEADER_FILES} ${CLIENT_SOURCE_FILES} ${CLIENT_RESOURCE_FILES})
target_compile_definitions(fsaClientTiny PUBLIC SIZE_OPTIMIZATION)

# build blingfire compile
add_library(fsaCompile STATIC ${CLIENT_HEADER_FILES} ${COMPILE_SOURCE_FILES} ${COMPILE_RESOURCE_FILES})
add_dependencies(fsaCompile fsaClient)

IF (UNIX OR LINUX OR ANDROID)
  set_target_properties(fsaClient PROPERTIES LINKER_LANGUAGE CXX)
  set_target_properties(fsaCompile PROPERTIES LINKER_LANGUAGE CXX)
ENDIF()

# Macro for finding the sub directories
MACRO(SUBDIRLIST result curdir)
  FILE(GLOB children RELATIVE ${curdir} ${curdir}/*)
  SET(dirlist "")
  FOREACH(child ${children})
    IF(IS_DIRECTORY ${curdir}/${child})
      LIST(APPEND dirlist ${child})
    ENDIF()
  ENDFOREACH()
  SET(${result} ${dirlist})
ENDMACRO()

# find all tools dirs
SUBDIRLIST(SUBDIRS ${CMAKE_CURRENT_SOURCE_DIR}/blingfiretools)

include(GNUInstallDirs)

# build tools
FOREACH(dir ${SUBDIRS})
  get_filename_component(dirname ${dir} NAME)
    file(GLOB sourcefile ${CMAKE_CURRENT_SOURCE_DIR}/blingfiretools/${dir}/*.cpp)
    file(GLOB resourcefile  ${CMAKE_CURRENT_SOURCE_DIR}/blingfiretools/${dir}/*.cxx)
    file(GLOB deffile ${CMAKE_CURRENT_SOURCE_DIR}/blingfiretools/${dir}/*.def)
    IF(${dirname} STREQUAL "blingfiretokdll")
      file(GLOB headerfile ${CMAKE_CURRENT_SOURCE_DIR}/blingfiretools/${dir}/*.h)

      IF(NOT IOS)
        add_library(${dirname} SHARED ${sourcefile} ${headerfile} ${resourcefile} ${deffile})
        target_link_libraries(${dirname} PRIVATE fsaClient)
      ENDIF()
      add_library(${dirname}_static ${sourcefile} ${resourcefile} ${deffile})
      INSTALL(
        FILES ${headerfile}
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR})

      target_link_libraries(${dirname}_static fsaClient)

      # A tiny binary
      IF(NOT IOS)
        add_library(bingfirtinydll SHARED ${sourcefile} ${deffile})
        target_link_libraries(bingfirtinydll fsaClientTiny)
      ENDIF()
      add_library(bingfirtinydll_static STATIC ${sourcefile} ${deffile})
      target_link_libraries(bingfirtinydll_static fsaClientTiny)
    ELSE()
      add_executable(${dirname} ${sourcefile} ${resourcefile} ${deffile})
      target_link_libraries(${dirname} fsaCompile fsaClient)
      IF(${dirname} STREQUAL "any_test")
        target_link_libraries(${dirname} ${CMAKE_DL_LIBS})
      ENDIF()
      IF (APPLE)
        target_link_libraries(${dirname} iconv)
      ENDIF()
    ENDIF()
ENDFOREACH()

IF(IOS)
  set(_merged_lib "${CMAKE_BINARY_DIR}/libblingfire.a")
  add_custom_command(
    TARGET blingfiretokdll_static POST_BUILD
    COMMAND libtool -static
            -o "${_merged_lib}"
            "$<TARGET_FILE:blingfiretokdll_static>"
            "$<TARGET_FILE:fsaClient>"
    COMMENT "Merging blingfiretokdll_static and fsaClient into libblingfire.a"
  )

  set(_merged_tiny_lib "${CMAKE_BINARY_DIR}/libblingfiretiny.a")
  add_custom_command(
    TARGET bingfirtinydll_static POST_BUILD
    COMMAND libtool -static
            -o "${_merged_tiny_lib}"
            "$<TARGET_FILE:bingfirtinydll_static>"
            "$<TARGET_FILE:fsaClientTiny>"
    COMMENT "Merging bingfirtinydll_static and fsaClientTiny into libblingfiretiny.a"
  )
ENDIF()

IF(NOT IOS)
  INSTALL(
    TARGETS blingfiretokdll
    EXPORT blingfire-targets
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
  )
ELSE()
  INSTALL(
    TARGETS blingfiretokdll_static fsaClient bingfirtinydll_static fsaClientTiny
    EXPORT blingfire-targets
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
  )
  INSTALL(
    FILES "${_merged_lib}" "${_merged_tiny_lib}"
    DESTINATION ${CMAKE_INSTALL_LIBDIR}
  )
ENDIF()

include(CMakePackageConfigHelpers)
write_basic_package_version_file(
  "${PROJECT_BINARY_DIR}/cmake/blingfire-config-version.cmake"
  VERSION ${BLING_FIRE_VERSION}
  COMPATIBILITY AnyNewerVersion
)

configure_file(${PROJECT_SOURCE_DIR}/cmake/blingfire-config.cmake.in
  ${PROJECT_BINARY_DIR}/cmake/blingfire-config.cmake
  COPYONLY
)
install(FILES ${PROJECT_BINARY_DIR}/cmake/blingfire-config.cmake
  ${PROJECT_BINARY_DIR}/cmake/blingfire-config-version.cmake
  DESTINATION ${CMAKE_INSTALL_DATAROOTDIR}/blingfire
)

install(EXPORT blingfire-targets
  DESTINATION ${CMAKE_INSTALL_DATAROOTDIR}/blingfire
)
