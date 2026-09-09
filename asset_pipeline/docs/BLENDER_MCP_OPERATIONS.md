# Blender MCP Operations

This document defines the bounded operational procedure for Blender MCP work.

## Expected environment

Current standard Blender version:

```text
Blender 5.2
```

Default MCP endpoint:

```text
127.0.0.1:9876
```

On the current Windows workstation, Blender may require:

```text
TBB_MALLOC_DISABLE_REPLACEMENT=1
```

before launch.

The normal installed Blender executable is expected at:

```text
C:\Program Files\Blender Foundation\Blender 5.2\blender.exe
```

Do not hard-code user-profile-specific paths into reusable pipeline scripts.

## Preflight health check

Before substantive asset work:

1. check whether Blender is already running;
2. check whether TCP port `9876` is listening;
3. perform one minimal MCP `get_scene_info` request;
4. continue only after the request succeeds.

Do not restart a healthy Blender instance simply because a new task begins.

## Recovery procedure

If the MCP connection fails:

1. attempt a normal reconnect;
2. check whether Blender is running;
3. check whether port `9876` is listening;
4. if Blender is not running, launch the normal installed Blender with the required environment variable;
5. wait for initialization;
6. test `get_scene_info` again;
7. if Blender runs but the MCP server is not listening, stop and report that MCP needs to be started or inspected.

## Prohibited recovery behavior

An MCP connection failure does **not** authorize an agent to:

- reinstall Blender;
- create a second Blender installation;
- copy Blender executables into another directory;
- copy or replace system/runtime DLLs;
- modify CRT manifests;
- alter Windows system files;
- perform broad application repair;
- make unrelated system-level changes.

If the bounded recovery procedure fails, stop and report the failure rather than escalating autonomously.

## Scene safety

Before destructive scene operations, determine whether the current scene contains user work that must be preserved.

Do not run factory reset or clear the scene blindly when an existing source pack may be open.

Use temporary scenes/collections/objects for validation and export isolation whenever practical.
