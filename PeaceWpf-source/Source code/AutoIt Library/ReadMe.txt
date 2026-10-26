Pal, Peter's AutoIt Library

Library of functions on the following topics:
- window, desktop and monitor
- mouse and gui settings such as color
- GUI controls such as png/jpg buttons and hyperlinks
- GUI numberbox controls for integer, real, bit and hexadecimal input
- logics and mathematics such as clamping, tweening and constants
- string, xml string and file string manipulation
- convenient dialogues such as Ok, Sure, download and progress bar
- data lists: lists, stacks, shift registers and key maps
- logging/debugging functions
- process and system info

Installation: Download zip file and unzip to a folder like My Documents/AutoIt/Pal
Documentation: Double click on Pal.chm. Don't see contents? Go to File properties of Pal.chm and click Unblock button
                 (a chm file needs rights to show the internal html pages)
               or unzip file htmlhelp and open index.htm in a browser.

Easy access in SciTe: Start SciTe Config in Tools menu and set User Include Folder to the unzip folder for instance My Documents/AutoIt/Pal
Call Tips: Go to Tools tab in SciTe Config and run User Calltip Manager

Version 1.30
Released 2025-12-10
- added: _WindowsVersionWorld() reads Windows version from registry as presented to the world
- added: _ProcessChildProcesses() gets child processes of given process (alternative for _WinAPI_EnumChildProcess(), using AutoIt ProcessList() and _WinAPI_GetParentProcess())
- added: _ProcessChildProcess() gets first child process of given process
improved: _WindowFromProcessId() now has a parameter for getting only visible windows
- added: _WindowDWMSetColors() to set title background and text color of a window, Windows 11 and higher
- added: _StringSwapParts() for swapping parts of a string on a given cut position
- added: _FileProperName() for stripping illegal characters from file name
- improved: _RegistryDelete() can also delete an entire key
- improved: _InList() may have 15 arguments
- bug: _RegistryWrite() didn't had a key type argument as described in RegWrite()
- bug: On the _Message() popup the "hide this message" label overlapped the Ok button a bit

Version 1.29
Released 2024-09-30
- added: _GUICtrlGetType() to get the type of a GUI control
- added: _WindowUnderMouse() to get topmost window under mouse pointer
- added: _VersionHigher() checks which of 2 given version numbers is higher
- improved: more keywords for searching a function in manual

Version 1.28
Released 2024-05-18
- added: $NB_NOREGISTER to indicate that own WM_COMMAND handler is being used instead of number box handler 
- added: _NumberBoxUnregister() to unregister number input boxes message handler and/or reset variables 
- added: _NumberBoxPushCounter() to push the number input boxes counter to the position stack 
- added: _NumberBoxPopCounter() to pop the number input boxes counter from the position stack removing the number input boxes pushed

Version 1.27
Released 2022-11-14
- added: Number input box library for entering decimal, binary and hexadecimal numbers in input boxes (examples in GUIExamples() function)
- added: _BitsToNumber() to convert a binary bit string to a decimal or string number
- added: _NumberToBits() to convert a binary bit string to decimal or string number
- added: _StringZero() for zero padding with given width of a number after converting it to a string

Version 1.26
Released 2021-12-23
- improved: _WindowsVersion() correctly returns the version for Windows 10 and 11 (by reading the new key value)
- added: _RegistryDelete() and _RegistryWrite() for deleting and writing a registry key item taking in account a 64 bit Windows
- improved: _RegistryRead() now returns @error and @extended as the RegRead() function

Version 1.25
Released 2021-03-19
- added: _GuiResize() to resize (client) size of GUI by absolute or delta
- added: _Decimals() to get the decimals of a number, converting strings to number
- added: _BitSet to set/unset a bit in a number (byte, word, etc.)
- added: _IsBit() to get a bit in a number (byte, word, etc.) is set, short for BitAnd( 2^($iBit-1), $nNumber ) > 0 ? 1 : 0

Version 1.24
Released 2021-03-04
- improved: _GUIGetBkColor() is also able to get initial background of window by its class properties
- added: _WindowExists() to check if a window exists
- added: _WindowVisible() to check if a window is visible
- added: _WindowEnabled() to check if a window is enabled
- added: _WindowMinimized() to check if a window is minimized
- added: _WindowMaximized() to check if a window is maximized
- changed: _StringTidyUp() rewritten. It didn't work properly
- improved: _StringTidyUp() has a second argument for not stripping leading spaces

Version 1.23
Released 2021-02-12
- added: _WindowDWMX() to get x coordinate of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
- added: _WindowDWMY() to get y coordinate of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
- added: _WindowDWMWidth() to get width of window as registered by to the Desktop Window Manager (DWM), Windows Vista and higher
- added: _WindowDWMHeight() to get height of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
- improved: _WindowGetX() and _WindowGetY() return Desktop Window Manager coordinates on Windows 10 when second argument is True
- improved: For _MessageBox(), _HideMessage() and _Message() a text and background color can be specified
- improved: Window a little bit enlarged of _HideMessage() and _Message() to hold longer text messages

Version 1.22
Released 2020-12-01
- improved: _Message() returns when the hide message dialogue has been closed by window close button
- added: _LogX() to calculate logarithm base X of given value
- added:  _WindowsScrollInactiveWindow() to read Windows setting to scroll an inactive window

Version 1.21
Released 2020-04-26
- bug: _WindowForceToPrimaryMonitor() WindowWidth and WindowHeight should be _WindowWidth and _WindowHeight

Version 1.20
Released 2020-02-23
- added:  _WindowForceToPrimaryMonitor() forces window to primary monitor if window isn't on target monitor any more due to monitor deactivation, desktop setting changed to 'Duplicate', etc.
- added: _StringStartsWith() for testing if a string starts with a string
- added: _StringEndsWith() for testing if a string ends with a string
- manual: _StringDiffer() explanation is now okay

Version 1.19
Released 2020-01-25
- added: _ProcessRunsAlready() returns if a process is already running when the same process is started again (singleton), process name may include path
- added: _ProcessInstances() gets number of instances of a process or processes, process name may include path
- added: _FileComparePaths() for comparing (url) paths (case-insensitive, last slash-insensitive)
- improved: _ProcessGetProcessId(), process name may include path
- improved: added a boolean parameter for _FilePath() to return lower case path (for easy comparing paths)
- improved: added type conversion parameter for _StringToArray() to convert column according to format: c = string, b = boolean, n = number, d = binary
- bug: for url's paths _FilePath() returned with forward slash
- bug: _StringToArray() didn't always return all columns for a 2D row/column split
- manual: some textual additions and function descriptions added
- manual: improved look of list examples
- manual: keywords added

Version 1.18
Released 2020-01-10
- added: _SoundGetWaveVolume() to get app volume of script (Windows Vista, 7, 8, 10 only)

Version 1.17
Released 2018-09-24
- added: _WindowsBuildNumber() and _WindowsUBRNumber() to read build and update build release numbers. Can be used to determine if Windows has been updated

Version 1.16
Release 2018-07-01
- added: _FileGetSizeTimed() tries to get file size in a loop when FileGetSize() fails (when Windows isn't done yet) for instance just after downloading the file
- improved: hovering about a _GUICtrlLinkLabel_Create() control will set mouse pointer to hand icon
- improved: calling _ProgressBarType() when bar color set to -1 default color is used

Version 1.15
Release 2018-01-24
- added: _WindowChanged() to check if window has been moved or resized and updates given array
- added: _WindowMenuHeight() to get the height of the menu bar
- added: _GUICtrlTrimSpaces() to trim leading and trailing spaces from a edit or input control
- added: _GUICtrlEmpty() to test if edit/input control is empty after stripping spaces, tabs, lf's, cr's
- improved: _WindowGetX() and _WindowGetY() if window doesn't exist return 0
- changed: second parameter of function _WindowFromProcessId() is now a keyword for searching the wanted window
- manual: more keywords added

Version 1.14
Release 2017-11-23
- added: _ProcessGetProcessId to get process id of a process name 
- added: _WindowFromProcessId to get window handle from process id
- added: _ColorBGRtoRGB to convert a BGR color to RGB color or vice versa
- manual: index of keywords added

Version 1.13
Release 2017-11-06
- added: _GUICtrlIsState() to check a control state of all its states
- added: _GUICtrlChangeState() to change a state only if the control hasn't it to prevent refreshing (flinkering) of the control
- added: _BitTest() to check if a bit is set

Version 1.12
Release 2017-11-01
- improved: _Message() and _HideMessage() may be called with an icon for instance for warning purposes
- improved: example shows an icon in _Message() example
- improved: explanation gdi+ initialization when using graphical buttons

Version 1.11
Release 2017-10-24
- added: function wrappers added for easy usage of ControlGetPos()

Version 1.10
Release 2017-10-16
- added: List functions for storing values and easy retrieval and manipulation
- improved: VarTypeGet added to compare variant variables in list, stack, shift register and map functions such as _StackValueInStack()

Version 1.00
Release 2017-09-05
- Library creation