#include-once
#include <GUIConstants.au3>
#include <GuiComboBox.au3>
#include <GuiListBox.au3>
#include <GDIPlus.au3>
#include <ColorConstants.au3>
#include <FontConstants.au3>
#include <WinAPI.au3>
#include <WinAPISys.au3>
#include <WinAPIGdi.au3>
#include <WinAPISysWin.au3>
#include <WinAPIHObj.au3>
#include <Math.au3>
#include <WinAPISysInternals.au3>

; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.30
; Description....: Window, Screen, Mouse and Control Functions
; Author(s)......: Peter Verbeek
; Version history: 1.30		added: _WindowDWMSetColors() to set title background and text color of a window, Windows 11 and higher
;                       	improved: _WindowFromProcessId() now has a parameter for getting only visible windows
;                  1.29		added: _WindowUnderMouse() to get topmost window under mouse pointer
;                       	added: _GUICtrlGetType() to get the type of a GUI control
;                  1.25		added: _GuiResize() to resize (client) size of GUI by absolute or delta
;                  1.24		improved: _GUIGetBkColor() is also able to get initial background of window by its class properties
;                      		added: _WindowExists() to check if a window exists
;                      		added: _WindowVisible() to check if a window is visible
;                      		added: _WindowEnabled() to check if a window is enabled
;                      		added: _WindowMinimized() to check if a window is minimized
;                      		added: _WindowMaximized() to check if a window is maximized
;                  1.23		added: _WindowDWMX() to get x coordinate of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;					   		added: _WindowDWMY() to get y coordinate of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;					   		added: _WindowDWMWidth() to get width of window as registered by to the Desktop Window Manager (DWM), Windows Vista and higher
;					   		added: _WindowDWMHeight() to get height of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;                       	improved: _WindowGetX() and _WindowGetY() return Desktop Window Manager coordinates on Windows 10 when second argument is True
;                  1.21		bug: _WindowForceToPrimaryMonitor() WindowWidth and WindowHeight should be _WindowWidth and _WindowHeight
;                  1.20		added: _WindowForceToPrimaryMonitor() forces window to primary monitor if window isn't on target monitor any more due to monitor deactivation, desktop setting changed to 'Duplicate', etc.
;                  1.17		bug: _WindowGetBkColor returned BGR color instead of RGB
;                  1.16 	improved: hovering about a _GUICtrlLinkLabel_Create() control will set mouse pointer to hand icon
;                  1.15		added: _WindowChanged() to check if window has been moved or resized and updates given array
;                      		added: _WindowMenuHeight() to get the height of the menu bar
;                       	added: _GUICtrlTrimSpaces() to trim leading and trailing spaces from a edit or input control
;                       	added: _GUICtrlEmpty() to test if edit/input control is empty after stripping spaces, tabs, LF's, CR's
;                      		improved: _WindowGetX() and _WindowGetY() if window doesn't exist return 0
;                      		changed: second parameter of _WindowFromProcessId() is now a keyword title
;                  1.14		added: _BGRtoRGB() to convert BGR color to RGB
;                       	added: _WindowFromProcessId() to get process id of a window
;                  1.13		added: _GUICtrlIsState() and _GUICtrlChangeState() added
;                  1.12		improved: explanation gdi+ initialization when using graphical buttons
;                  1.11		added: function wrappers added for easy usage of ControlGetPos()
;                  1.00		initial version
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;	_WindowWidth
;	_WindowHeight
;	_WindowDWMWidth
;	_WindowDWMHeight
;	_WindowClientWidth
;	_WindowClientHeight
;	_WindowBordersWidth
;	_WindowBordersHeight
;	_WindowMenuHeight
;	_WindowGetX
;	_WindowGetY
;	_WindowDWMX
;	_WindowDWMY
;	_WindowDWMSetColors
;	_WindowGetBkColor
;	_Control3DBorderWidth
;	_Control3DBorderHeight
;	_WindowIsState
;	_WindowExists
;	_WindowVisible
;	_WindowEnabled
;	_WindowMinimized
;	_WindowMaximized
;	_WindowChanged
;	_WindowFromProcessId
;	_WindowUnderMouse
;	_WindowForceToPrimaryMonitor
;	_DesktopWorkAreaWidth
;	_DesktopWorkAreaHeight
;	_GUIMouseControl
;	_GUIMouseDown
;	_GUIMouseGetX
;	_GUIMouseGetY
;	_GUIResize
;	_GUIGetBkColor
;	_ColorMix
;	_ColorBrighten
;	_ColorBGRtoRGB
;	_GUICtrlGetType
;	_GUICtrlEmpty
;	_GUICtrlTrimSpaces
;	_GUICtrlIsState
;	_GUICtrlChangeState
;	_GUICtrlLeft
;	_GUICtrlRight
;	_GUICtrlTop
;	_GUICtrlBottom
;	_GUICtrlWidth
;	_GUICtrlHeight
;	_GUICtrlComboBox_AddArray
;	_GUICtrlListBox_AddArray
;	_GUICtrlLinkLabel_Create
;	_GUICtrlLinkLabel_Clicked
;	_GUICtrlLinkLabel_SetColor
;	_GUICtrlLinkLabel_SetClickedColorlabels
;	_GUICtrlLinkLabel_SetFontSize
;	_GUICtrlLinkLabel_GetFontSize
;	_GraphicButtons
;	_GraphicButtonsCount
;	_GraphicButtonsSetBkColor
;	_GraphicButton
;	_GraphicButtonWidth
;	_GraphicButtonHeight
;	_GraphicButtonText
;	_GraphicButtonTooltip
; ===============================================================================================================================

; List of 65 functions
;	_WindowWidth						Window					Returns window width, client size + border
;	_WindowHeight						Window					Returns window height, client size + border
;	_WindowDWMWidth						Window					Returns width of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;	_WindowDWMHeight					Window					Returns height of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;	_WindowClientWidth					Window					Returns width of window client area
;	_WindowClientHeight					Window					Returns height of window client area
;	_WindowBordersWidth					Window					Returns total width of window borders
;	_WindowBordersHeight				Window					Returns total height of window borders
;	_WindowMenuHeight					Window					Returns height of menu bar
;	_WindowGetX							Window					Returns x position of window on screen
;	_WindowGetY							Window					Returns y position of window on screen
;	_WindowDWMX							Window					Returns x coordinate of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;	_WindowDWMY							Window					Returns y coordinate of window as registered by the Desktop Window Manager (DWM), Windows Vista and higher
;	_WindowDWMSetColors					Window					Sets the background and/or text color of the window title bar through the Desktop Window Manager (DWM), Windows 11 and higher
;	_WindowGetBkColor					Window					Returns default window background color
;	_Control3DBorderWidth				Window/Control			Returns width of window/control border, small windows and controls such as list boxes
;	_Control3DBorderHeight				Window/Control			Returns height of window/control border, small windows and controls such as list boxes
;	_WindowIsState						Window					Returns if window is in given state
;	_WindowExists						Window					Returns if window exists
;	_WindowVisible						Window					Returns if window is visible
;	_WindowEnabled						Window					Returns if window is enabled
;	_WindowMinimized					Window					Returns if window is minimized
;	_WindowMaximized					Window					Returns if window is maximized
;	_WindowChanged						Window					Returns if window has been moved or resized, checking only if mouse button goes up on given window
;	_WindowFromProcessId				Window					Gets the handle of the first window or window with title keyword of given process id
;	_WindowUnderMouse					Window/Mouse			Gets window under mouse pointer
;	_WindowForceToPrimaryMonitor		Window					Forces window to primary monitor if window isn't on target monitor any more due to monitor deactivation, desktop setting changed to 'Duplicate', etc.
;	_DesktopWorkAreaWidth				Desktop & Screen		Returns work area width of desktop under mouse
;	_DesktopWorkAreaHeight				Desktop & Screen		Returns work area height of desktop under mouse
;	_GUIMouseControl					Mouse					Returns handle of control mouse is hovering over
;	_GUIMouseDown						Mouse					Returns if primary or secondary mouse button is down, by default primary = left button, secondary = right button
;	_GUIMouseGetX						Mouse					Returns mouse x position on GUI
;	_GUIMouseGetY						Mouse					Returns mouse y position on GUI
;	_GUIResize							GUI						Resizes (client) size of GUI by absolute or delta
;	_GUIGetBkColor						GUI						Returns GUI window background color, remark: gui window must be visible
;	_ColorMix							Color					Returns mix of 2 colors
;	_ColorBrighten						Color					Returns brightened or darkened color
;	_ColorBGRtoRGB						Color					Converts RGB color from BGR color or vice versa
;	_GUICtrlGetType						Control					Gets the type of a GUI control
;	_GUICtrlEmpty						Control					Tests if edit or input control is empty after stripping spaces, tabs, line feeds or carriage returns
;	_GUICtrlTrimSpaces()				Control					Trims leading and trailing spaces from a edit or input control
;	_GUICtrlIsState						Control					Checks if all states of a control if one state is present
;	_GUICtrlChangeState					Control					Changes control to given state if hasn't been set yet, preventing a refresh (flickering)
;	_GUICtrlLeft						Control					Gets left position of given control in given window
;	_GUICtrlRight						Control					Gets right position of given control in given window
;	_GUICtrlTop							Control					Gets top position of given control in given window
;	_GUICtrlBottom						Control					Gets bottom position of given control in given window
;	_GUICtrlWidth						Control					Gets width position of given control in given window
;	_GUICtrlHeight						Control					Gets height position of given control in given window
;	_GUICtrlComboBox_AddArray			Control					Fills combobox control with array
;	_GUICtrlListBox_AddArray			Control					Fills listbox control with array
;	_GUICtrlLinkLabel_Create			Control					Creates a hyperlink label
;	_GUICtrlLinkLabel_Clicked			Control					Opens given URL and shows user has clicked link
;	_GUICtrlLinkLabel_SetColor			Control					Sets color of unopened (not clicked yet) labels
;	_GUICtrlLinkLabel_SetClickedColor	Control					Sets color of clicked/opened URL labels
;	_GUICtrlLinkLabel_SetFontSize		Control					Sets font size of link labels
;	_GUICtrlLinkLabel_GetFontSize		Control					Gets font size of link labels
;	_GraphicButtons						Control					Initializes graphic buttons (buttons with a png or jpg image) or cleans up resources
;	_GraphicButtonsCount				Control					Returns number of graphic buttons
;	_GraphicButtonsSetBkColor			Control					Sets background color of window for conversion of png/jpg to bitmap
;	_GraphicButton						Control					Creates graphic button or graphic image
;	_GraphicButtonWidth					Control					Returns graphic button width or image width
;	_GraphicButtonHeight				Control					Returns graphic button height or image height
;	_GraphicButtonText					Control					Returns text on graphic button
;	_GraphicButtonTooltip				Control					Returns tooltip of graphic button

Global Const $GUI_TYPE_FAILURE = -1
Global Const $GUI_TYPE_UNKNOWN = 0
Global Const $GUI_TYPE_LABEL = 10
Global Const $GUI_TYPE_PIC = 11
Global Const $GUI_TYPE_ICON = 12
Global Const $GUI_TYPE_BUTTON = 20
Global Const $GUI_TYPE_CHECKBOX = 21
Global Const $GUI_TYPE_RADIO = 22
Global Const $GUI_TYPE_GROUP = 23
Global Const $GUI_TYPE_PUSHBUTTON = 24
Global Const $GUI_TYPE_LISTBOX = 30
Global Const $GUI_TYPE_COMBOBOX = 40
Global Const $GUI_TYPE_LISTVIEW = 50
Global Const $GUI_TYPE_EDIT = 60
Global Const $GUI_TYPE_INPUT = 61
Global Const $GUI_TYPE_SLIDER = 70
Global Const $GUI_TYPE_PROGRESSBAR = 80
Global Const $GUI_TYPE_TAB = 90
Global Const $GUI_TYPE_TREEVIEW = 100
Global Const $GUI_TYPE_DATETIME = 110
Global Const $GUI_TYPE_CALENDAR = 120
Global Const $GUI_TYPE_COMBOBOXEX = 200
Global Const $GUI_TYPE_ANIMATION = 210
Global Const $GUI_TYPE_HEADER = 220
Global Const $GUI_TYPE_IPADDRESS = 230
Global Const $GUI_TYPE_REBAR = 240
Global Const $GUI_TYPE_TOOLBAR = 250
Global Const $GUI_TYPE_STATUSBAR = 260
Global Const $GUI_TYPE_TOOLTIP = 270

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowWidth
; Description....: Returns window width, client size + border
; Syntax.........: _WindowWidth($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Window width
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowWidth($hGUI)
	Local $aGUI = WinGetClientSize($hGUI)
	Return $aGUI[0]+(_WindowIsState($hGUI,$WIN_STATE_MAXIMIZED) ? 0 : 2*_WinAPI_GetSystemMetrics($SM_CXFRAME))	; window width of default window style
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowHeight
; Description....: Returns window height, client size + border
; Syntax.........: _WindowHeight($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Window height
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowHeight($hGUI)
	Local $aGUI = WinGetClientSize($hGUI)
	Return $aGUI[1]+_WinAPI_GetSystemMetrics($SM_CYCAPTION)+2*_WinAPI_GetSystemMetrics($SM_CYFRAME)	; window height of default window style with minimize box, etc.
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowClientWidth
; Description....: Returns width of window client area
; Syntax.........: _WindowClientWidth($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Width of window client area
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowClientWidth($hGUI)
	Local $aGUI = WinGetClientSize($hGUI)
	Return $aGUI[0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowClientHeight
; Description....: Returns height of window client area
; Syntax.........: _WindowClientHeight($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Height of window client area
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowClientHeight($hGUI)
	Local $aGUI = WinGetClientSize($hGUI)
	Return $aGUI[1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowBordersWidth
; Description....: Returns total width of window borders
; Syntax.........: _WindowBordersWidth()
; Parameters.....: None
; Return values..: Total width of window borders
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowBordersWidth()
	Return 2*_WinAPI_GetSystemMetrics($SM_CXFRAME)	; window borders width of default window style
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowBordersHeight
; Description....: Returns total height of window borders
; Syntax.........: _WindowBordersHeight()
; Parameters.....: None
; Return values..: Total height of window borders
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowBordersHeight()
	Return _WinAPI_GetSystemMetrics($SM_CYCAPTION)+2*_WinAPI_GetSystemMetrics($SM_CYFRAME)	; window borders height of default style with mimimize box, etc.
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Control3DBorderWidth
; Description....: Returns width of 3D window/control border, small windows and controls such as list boxes
; Syntax.........: _Control3DBorderWidth()
; Parameters.....: None
; Return values..: Width of 3D window/control border
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Control3DBorderWidth()
	Return _WinAPI_GetSystemMetrics($SM_CXEDGE)	; 3D window/control border width of default window style
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Control3DBorderHeight
; Description....: Returns height of 3D window/control border, small windows and controls such as list boxes
; Syntax.........: _Control3DBorderHeight()
; Parameters.....: None
; Return values..: Height of 3D window/control border
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Control3DBorderHeight()
	Return _WinAPI_GetSystemMetrics($SM_CYEDGE)	; 3D window/control border height of default window style
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowMenuHeight
; Description....: Returns height of menu bar
; Syntax.........: _WindowMenuHeight()
; Parameters.....: None
; Return values..: Height of menu bar
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowMenuHeight()
	Return _WinAPI_GetSystemMetrics($SM_CYMENU)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowGetX
; Description....: Returns x position of window on screen, may be the x position as registered by the Desktop Window Manager
; Syntax.........: _WindowGetX($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
;                  $UseDWMCoordinates		- True = Use Desktop Window Manager coordinates Windows Vista and higher
; Return values..: Window x position of window on screen
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowGetX($hGUI,$UseDWMCoordinates = False)
	If $UseDWMCoordinates Then
		; Try finding X coordinate by the Desktop Window Manager, Windows Vista and higher
		Local $tagRect = "struct;long Left;long Top;long Right;long Bottom;endstruct",$tRect = DllStructCreate($tagRect)
		DllCall("Dwmapi.dll","long","DwmGetWindowAttribute","hwnd",WinGetHandle($hGUI),"dword",9,"ptr",DllStructGetPtr($tRect),"dword",DllStructGetSize($tRect))
		If @error = 0 And DllStructGetData($tRect,3)-DllStructGetData($tRect,1) > 0 And DllStructGetData($tRect,4)-DllStructGetData($tRect,2) > 0 Then Return DllStructGetData($tRect,1)
	EndIf
	; Alternatively return X coordinate by AutoIt
	Local $aWindow = WinGetPos($hGUI)
	If @error <> 0 Or $aWindow[0] = -32000 Then Return 0				; correcting for a minimized window
	Return $aWindow[0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowGetY
; Description....: Returns y position of window on screen, may be the y position as registered by the Desktop Window Manager
; Syntax.........: _WindowGetY($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
;                  $UseDWMCoordinates		- True = Use Desktop Window Manager coordinates Windows Vista and higher
; Return values..: Window y position of window on screen
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowGetY($hGUI,$UseDWMCoordinates = False)
	If $UseDWMCoordinates Then
		; Try finding Y coordinate by the Desktop Window Manager, Windows Vista and higher
		Local $tagRect = "struct;long Left;long Top;long Right;long Bottom;endstruct",$tRect = DllStructCreate($tagRect)
		DllCall("Dwmapi.dll","long","DwmGetWindowAttribute","hwnd",WinGetHandle($hGUI),"dword",9,"ptr",DllStructGetPtr($tRect),"dword",DllStructGetSize($tRect))
		If @error = 0 And DllStructGetData($tRect,3)-DllStructGetData($tRect,1) > 0 And DllStructGetData($tRect,4)-DllStructGetData($tRect,2) > 0 Then Return DllStructGetData($tRect,2)
	EndIf
	; Alternatively return Y coordinate by AutoIt
	Local $aWindow = WinGetPos($hGUI)
	If @error <> 0 Or $aWindow[1] = -32000 Then Return 0				; correcting for a minimized window
	Return $aWindow[1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowDWMWidth
; Description....: Returns window width as registered by the Desktop Window Manager (DWM)
; Syntax.........: _WindowDWMWidth($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Desktop Window Manager window width
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowDWMWidth($hGUI)
	; Try finding width by the Desktop Window Manager, Windows Vista and higher
	Local $tagRect = "struct;long Left;long Top;long Right;long Bottom;endstruct",$tRect = DllStructCreate($tagRect)
	DllCall("Dwmapi.dll","long","DwmGetWindowAttribute","hwnd",WinGetHandle($hGUI),"dword",9,"ptr",DllStructGetPtr($tRect),"dword",DllStructGetSize($tRect))
	If @error = 0 And DllStructGetData($tRect,3)-DllStructGetData($tRect,1) > 0 And DllStructGetData($tRect,4)-DllStructGetData($tRect,2) > 0 Then Return DllStructGetData($tRect,3)-DllStructGetData($tRect,1)
	; Alternatively return window width by AutoIt
	Local $aWindow = WinGetPos($hGUI)
	If @error <> 0 Or $aWindow[0] = -32000 Then Return 0				; correcting for a minimized window
	Return $aWindow[2]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowDWMHeight
; Description....: Returns window height as registered by the Desktop Window Manager (DWM)
; Syntax.........: _WindowDWMHeight($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Desktop Window Manager window height
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowDWMHeight($hGUI)
	; Try finding width by the Desktop Window Manager, Windows Vista and higher
	Local $tagRect = "struct;long Left;long Top;long Right;long Bottom;endstruct",$tRect = DllStructCreate($tagRect)
	DllCall("Dwmapi.dll","long","DwmGetWindowAttribute","hwnd",WinGetHandle($hGUI),"dword",9,"ptr",DllStructGetPtr($tRect),"dword",DllStructGetSize($tRect))
	If @error = 0 And DllStructGetData($tRect,3)-DllStructGetData($tRect,1) > 0 And DllStructGetData($tRect,4)-DllStructGetData($tRect,2) > 0 Then Return DllStructGetData($tRect,4)-DllStructGetData($tRect,2)
	; Alternatively return window height by AutoIt
	Local $aWindow = WinGetPos($hGUI)
	If @error <> 0 Or $aWindow[0] = -32000 Then Return 0				; correcting for a minimized window
	Return $aWindow[3]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowDWMX
; Description....: Returns window x coordinate as registered by the Desktop Window Manager (DWM)
; Syntax.........: _WindowDWMWidth($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Desktop Window Manager window x coordinate
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowDWMX($hGUI)
	; Try finding x coordinate by the Desktop Window Manager, Windows Vista and higher
	Local $tagRect = "struct;long Left;long Top;long Right;long Bottom;endstruct",$tRect = DllStructCreate($tagRect)
	DllCall("Dwmapi.dll","long","DwmGetWindowAttribute","hwnd",WinGetHandle($hGUI),"dword",9,"ptr",DllStructGetPtr($tRect),"dword",DllStructGetSize($tRect))
	If @error = 0 And DllStructGetData($tRect,3)-DllStructGetData($tRect,1) > 0 And DllStructGetData($tRect,4)-DllStructGetData($tRect,2) > 0 Then Return DllStructGetData($tRect,1)
	; Alternatively return window width by AutoIt
	Local $aWindow = WinGetPos($hGUI)
	If @error <> 0 Or $aWindow[0] = -32000 Then Return 0				; correcting for a minimized window
	Return $aWindow[0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowDWMY
; Description....: Returns window y coordinate as registered by the Desktop Window Manager (DWM)
; Syntax.........: _WindowDWMWidth($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Desktop Window Manager window y coordinate
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowDWMY($hGUI)
	; Try finding y coordinate by the Desktop Window Manager, Windows Vista and higher
	Local $tagRect = "struct;long Left;long Top;long Right;long Bottom;endstruct",$tRect = DllStructCreate($tagRect)
	DllCall("Dwmapi.dll","long","DwmGetWindowAttribute","hwnd",WinGetHandle($hGUI),"dword",9,"ptr",DllStructGetPtr($tRect),"dword",DllStructGetSize($tRect))
	If @error = 0 And DllStructGetData($tRect,3)-DllStructGetData($tRect,1) > 0 And DllStructGetData($tRect,4)-DllStructGetData($tRect,2) > 0 Then Return DllStructGetData($tRect,2)
	; Alternatively return window width by AutoIt
	Local $aWindow = WinGetPos($hGUI)
	If @error <> 0 Or $aWindow[0] = -32000 Then Return 0				; correcting for a minimized window
	Return $aWindow[1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowDWMSetColors
; Description....: Sets the background and/or text color of the window title bar through the Desktop Window Manager (DWM)
; Syntax.........: _WindowDWMSetColors($hGUI, $iBackgroundColor [, $iTextColor = -1])
; Parameters.....: $hGUI					- Window GUI handle
;                  $iBackgroundColor		- Title bar background color
;                  $iTextColor				- Title bar text color (-1 = no color is being set)
; Return values..: 0 = successful, 1 = background color couldn't be set, 2 = text color couldn't be set, 3 = both couldn't be set
; Remark.........: Only Windows 11 and higher allows this coloring
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowDWMSetColors($hGUI,$iBackgroundColor,$iTextColor = -1)
	Local $nError = 0
	; title bar background color
	If Dllcall("Dwmapi.dll","long","DwmSetWindowAttribute","hwnd",$hGUI,"dword",35,"dword*",_ColorBGRtoRGB($iBackgroundColor),"dword",4) <> $S_OK Then $nError = 1
	If $iTextColor = -1 Then Return
	; title bar text color
	If Dllcall("Dwmapi.dll","long","DwmSetWindowAttribute","hwnd",$hGUI,"dword",36,"dword*",_ColorBGRtoRGB($iTextColor),"dword",4) <> $S_OK Then $nError += 2
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowGetBkColor
; Description....: Returns default window background color
; Syntax.........: _WindowGetBkColor([$bFailSave = True])
; Parameters.....: $bFailSave				- True = In case of a black color returned, return Windows grey
; Return values..: Default window background color
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowGetBkColor($bFailSave = True)
	Local $iColor = _ColorBGRtoRGB(_WinAPI_GetSysColor($COLOR_MENU))				; color probably same as background color of popup menu ($COLOR_WINDOW gives wrong color)
	If $bFailSave And $iColor = 0 Then $iColor = 0xF0F0F0			; fail save in case black is returned
	Return $iColor
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowIsState
; Description....: Returns if window is in given state
; Syntax.........: _WindowIsState($hGUI, $iState)
; Parameters.....: $hGUI					- Window GUI handle
;                  $iState					- Window state
;												1 = $WIN_STATE_EXISTS
;												2 = $WIN_STATE_VISIBLE
;												4 = $WIN_STATE_ENABLED
;												8 = $WIN_STATE_ACTIVE
;												16 = $WIN_STATE_MINIMIZED
;												32 = $WIN_STATE_MAXIMIZED
; Return values..: True = In state $State, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowIsState($hGUI,$iState)
	Return BitAnd(WinGetState($hGUI),$iState) = $iState
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowExists
; Description....: Returns if window exists
; Syntax.........: _WindowExists($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: True = exists, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowExists($hGUI)
	Return _WindowIsState($hGUI,$WIN_STATE_EXISTS)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowVisible
; Description....: Returns if window is visible
; Syntax.........: _WindowVisible($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: True = visible, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowVisible($hGUI)
	Return _WindowIsState($hGUI,$WIN_STATE_VISIBLE)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowEnabled
; Description....: Returns if window is enabled
; Syntax.........: _WindowEnabled($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: True = enabled, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowEnabled($hGUI)
	Return _WindowIsState($hGUI,$WIN_STATE_ENABLED)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowMinimized
; Description....: Returns if window is minimized
; Syntax.........: _WindowMinimized($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: True = minimized, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowMinimized($hGUI)
	Return _WindowIsState($hGUI,$WIN_STATE_MINIMIZED)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowMaximized
; Description....: Returns if window is maximized
; Syntax.........: _WindowMaximized($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: True = maximized, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowMaximized($hGUI)
	Return _WindowIsState($hGUI,$WIN_STATE_MAXIMIZED)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowChanged
; Description....: Returns if window has been moved or resized and updates given array
; Syntax.........: _WindowChanged( $hGUI, $aFormerPosition [,$bClientArea = False ] )
; Parameters.....: $hGUI					- Window GUI handle
;                  $aFormerPosition			- Former position and size of given window
;                  $bClientArea				- True = given former position contains size of client area instead of window
; Return values..: True = Window has been moved or resized
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowChanged($hGUI, ByRef $aFormerPosition,$bClientArea = False)
	Local $aPosition = WinGetPos($hGUI),$BordersX = $bClientArea ? _WindowBordersWidth() : 0,$BordersY = $bClientArea ? _WindowBordersHeight() : 0
	If $aPosition[0] = -32000 Or $aPosition[1] = -32000 Then Return False	; -32000 means minimized window, don't check
	$aPosition[2] = Floor($aPosition[2])-$BordersX
	$aPosition[3] = Floor($aPosition[3])-$BordersY
	If $aFormerPosition[0] <> $aPosition[0] Or $aFormerPosition[1] <> $aPosition[1] Or Floor($aFormerPosition[2]) <> $aPosition[2] Or Floor($aFormerPosition[3]) <> $aPosition[3] Then
		$aFormerPosition[0] = $aPosition[0]
		$aFormerPosition[1] = $aPosition[1]
		$aFormerPosition[2] = $aPosition[2]
		$aFormerPosition[3] = $aPosition[3]
		Return True
	EndIf
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowFromProcessId
; Description....: Gets the handle of the first window or window with title keyword of given process id
; Syntax.........: _WindowFromProcessId( $ProcessId [, $cTitleKeyword = "" [, $EnabledOnly = True ]] )
; Parameters.....: $iProcessId				- Id of process
;                  $cTitleKeyword			- Keyword to match the window to be activated, usually the main window
;                  $bEnabledOnly			- True = (default) only an enabled window, False = an enabled/disabled window
;                  $bVisibleOnly			- True = only a visible window, False = (default) an invisible/visible window
; Return values..: First found window handle or null pointer if a window couldn't be found
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowFromProcessId($iProcessId,$cTitleKeyword = "",$bEnabledOnly = True,$bVisibleOnly = False)
	Local $aWindowList = WinList()
	For $iWindow = 1 To $aWindowList[0][0]
		; try existing and enabled windows
		If WinGetProcess($aWindowList[$iWindow][1]) = $iProcessId And (StringLen($cTitleKeyword) = 0 Or StringInStr($aWindowList[$iWindow][0],$cTitleKeyword) > 0) And _
		   _WindowIsState($aWindowList[$iWindow][1],1) And (Not $bEnabledOnly Or _WindowIsState($aWindowList[$iWindow][1],4)) And (Not $bVisibleOnly Or _WindowIsState($aWindowList[$iWindow][1],2)) Then Return $aWindowList[$iWindow][1]
	Next
	Return Null
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowUnderMouse
; Description....: Gets topmost window under mouse pointer
; Syntax.........: _WindowUnderMouse( [$hUser32DLL = -1] )
; Parameters.....: $hUser32DLL				- Handle to user32.dll file. When -1 file is opened by DllCall()
; Return values..: Window handle or 0 at error
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowUnderMouse($hUser32DLL = -1)
	Local $tPoint = DllStructCreate($tagPOINT)
    DllStructSetData($tPoint,"x",MouseGetPos(0))
    DllStructSetData($tPoint,"y",MouseGetPos(1))
	Local $aResult = DllCall(($hUser32DLL = -1 ? "user32.dll" : $hUser32DLL),"hwnd","WindowFromPoint","struct",$tPoint)
	If @error Then Return 0
	$aResult = DllCall(($hUser32DLL = -1 ? "user32.dll" : $hUser32DLL),"hwnd","GetAncestor","hwnd",$aResult[0],"uint",$GA_ROOT)
	If @error Then Return 0
	Return $aResult[0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowForceToPrimaryMonitor
; Description....: Forces window to primary monitor if window isn't on target monitor any more due to monitor deactivation, desktop setting changed to 'Duplicate', etc.
; Syntax.........: _WindowForceToPrimaryMonitor( $hGUI )
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: True if forced to primary monitor
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowForceToPrimaryMonitor($hGUI)
	$Forced = _WinAPI_MonitorFromWindow($hGUI,$MONITOR_DEFAULTTONULL) = 0
	If $Forced Then WinMove($hGUI,"",@DesktopWidth/2-_WindowWidth($hGUI)/2,@DesktopHeight/2-_WindowHeight($hGUI)/2)
	Return $Forced
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _DesktopWorkAreaWidth
; Description....: Returns work area width of desktop under mouse
; Syntax.........: _DesktopWorkAreaWidth()
; Parameters.....: None
; Return values..: Work area width of desktop under mouse
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _DesktopWorkAreaWidth()
	Local $tPos = _WinAPI_GetMousePos(),$hMonitor = _WinAPI_MonitorFromPoint($tPos),$aData = _WinAPI_GetMonitorInfo($hMonitor)
	If IsArray($aData) Then
		Return DllStructGetData($aData[1],3)-DllStructGetData($aData[1],1)
	Else
		Return @DesktopWidth
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _DesktopWorkAreaHeight
; Description....: Returns work area height of desktop under mouse
; Syntax.........: _DesktopWorkAreaHeight()
; Parameters.....: None
; Return values..: Work area height of desktop under mouse
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _DesktopWorkAreaHeight()
	Local $tPos = _WinAPI_GetMousePos(),$hMonitor = _WinAPI_MonitorFromPoint($tPos),$aData = _WinAPI_GetMonitorInfo($hMonitor)
	If IsArray($aData) Then
		Return DllStructGetData($aData[1],4)-DllStructGetData($aData[1],2)
	Else
		Return @DesktopHeight
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUIMouseControl
; Description....: Returns handle of control mouse is hovering over
; Syntax.........: _GUIMouseControl($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Control handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUIMouseControl($hGUI)
	Local $aMouse = GUIGetCursorInfo($hGUI)
	Return $aMouse[4]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUIMouseDown
; Description....: Returns if primary or secondary mouse button is down, by default primary= eft button, secondary=right button
; Syntax.........: _GUIMouseDown($hGUI [,$iButton = 1])
; Parameters.....: $hGUI					- Window GUI handle
;                  $iButton					- Primary button = 1, Secondary button = 2, by Windows default primary button is left
; Return values..: True = down, False = up
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUIMouseDown($hGUI,$iButton = 1)
	Local $aMouse = GUIGetCursorInfo($hGUI)
	If $iButton = 1 Then
		Return $aMouse[2] = 1
	Else
		Return $aMouse[3] = 1
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUIMouseGetX
; Description....: Returns mouse x position on GUI
; Syntax.........: _GUIMouseGetX($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Mouse x position on GUI
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUIMouseGetX($hGUI)
	Local $aMouse = GUIGetCursorInfo($hGUI)
	Return $aMouse[0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUIMouseGetY
; Description....: Returns mouse y position on GUI
; Syntax.........: _GUIMouseGetY($hGUI)
; Parameters.....: $hGUI					- Window GUI handle
; Return values..: Mouse y position on GUI
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUIMouseGetY($hGUI)
	Local $aMouse = GUIGetCursorInfo($hGUI)
	Return $aMouse[1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUIResize
; Description....: Resizes (client) size of GUI by absolute or delta
; Syntax.........: _GUIResize($hGUI,$nWidth,$nHeight [,$bDelta = False])
; Parameters.....: $hGUI					- Window GUI handle
;                  $nWidth					- GUI (client) width to resize to
;                  $nHeight					- GUI (client) height to resize to
;                  $bDelta					- False = Absolute width and height given, True = Delta width and height given (can be negative)
; Return values..: True = Window resized, False = Not
; @error.........: 0 = No error, 1 = Window minimized, 2 = Couldn't get window size, 3 = Couldn't get window client size
; Remark.........: Window mustn't be minimized
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUIResize($hGUI,$nWidth,$nHeight,$bDelta = False)
	If BitAnd(WinGetState($hGUI),$WIN_STATE_MINIMIZED) = $WIN_STATE_MINIMIZED Then Return SetError(1,0,False)

	Local $nWindowPosition,$nClientSize

	$nWindowPosition = WinGetPos($hGUI)
	If @error Then Return SetError(2,0,False)
	If $bDelta Then
		If $nWidth <> 0 Or $nHeight <> 0 Then WinMove($hGUI,"",Default,Default,$nWindowPosition[2]+$nWidth > 0 ? $nWindowPosition[2]+$nWidth : 1,$nWindowPosition[3]+$nHeight > 0 ? $nWindowPosition[3]+$nHeight : 1)
	Else
		$nClientSize = WinGetClientSize($hGUI)
		If @error Then Return SetError(3,0,False)
		If $nClientSize[0] <> $nWidth Or $nClientSize[1] <> $nHeight Then WinMove($hGUI,"",Default,Default,_Max($nWindowPosition[2]-$nClientSize[0]+$nWidth,1),_Max($nWindowPosition[3]-$nClientSize[1]+$nHeight,1))
	EndIf
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUIGetBkColor
; Description....: Returns GUI window background color
; Syntax.........: _GUIGetBkColor($hGUI [,$bUseWindowClass = False])
; Parameters.....: $hGUI					- Window GUI handle
;                  $bUseWindowClass			- False = Use display device context (current color), True = Use window class (initial color)
; Return values..: GUI window background color
; Remark.........: When using display device context the window has be to visible (drawn)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUIGetBkColor($hGUI,$bUseWindowClass = False)
	Local $iColor = 0,$Error = False
	If $bUseWindowClass Then
		; Get color from window class
		; https://devblogs.microsoft.com/oldnewthing/20190802-00/?p=102747
		Local $tLogBrush = DllStructCreate("uint lbStyle; dword lbColor; ulong_ptr lbHatch"),$hBrush = _WinAPI_GetClassLongEx($hGUI,$GCL_HBRBACKGROUND)
		If $hBrush = 0 Then
			$Error = True		; fall back to display device context color retrieving
		elseIf _WinAPI_GetObject($hBrush,DllStructGetSize($tLogBrush),DllStructGetPtr($tLogBrush)) = DllStructGetSize($tLogBrush) Then
			$iColor = DllStructGetData($tLogBrush,"lbColor")
		Else
			$Error = True		; fall back to display device context color retrieving
		EndIf
	Else
		$Error = True
	EndIf
	If $Error Then
		Local $hDC = _WinAPI_GetDC($hGUI)
		If $hDC <> 0 Then
			$iColor = _WinAPI_GetBkColor($hDC)
			If $iColor > -1 Then $Error = False
			_WinAPI_ReleaseDC($hGUI,$hDC)
		EndIf
	EndIf
	If $Error Then
		$iColor = _WinAPI_GetSysColor(4)				; color probably same as background color of popup menu $COLOR_MENU ($COLOR_WINDOW gives wrong color)
		If $iColor = 0 Then $iColor = 15790320	; fail save in case black is returned
	EndIf
	Return $iColor
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ColorMix
; Description....: Returns mix of 2 colors
; Syntax.........: _ColorMix($iColor1, $iColor2 [,nMix = 2))
; Parameters.....: $iColor1					- Color 1
;                  $iColor2					- Color 2
;                  $nMix					- Brighten or darken mixing of colors, 2 = average of colors
; Return values..: Mix of 2 colors
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ColorMix($iColor1,$iColor2,$nMix = 2)
	Local $iRed1 = BitAND(BitShift($iColor1,16),0xFF),$iRed2 = BitAND(BitShift($iColor2,16),0xFF),$iGreen1 = BitAND(BitShift($iColor1,8),0xFF),$iGreen2 = BitAND(BitShift($iColor2,8),0xFF),$iBlue1 = BitAnd($iColor1,0xFF),$iBlue2 = BitAnd($iColor2,0xFF)
	$iRed1 = ($iRed1+$iRed2)/$nMix
	If $iRed1 > 255 Then $iRed1 = 255
	$iGreen1 = ($iGreen1+$iGreen2)/$nMix
	If $iGreen1 > 255 Then $iGreen1 = 255
	$iBlue1 = ($iBlue1+$iBlue2)/$nMix
	If $iBlue1 > 255 Then $iBlue1 = 255
	Return BitShift($iRed1,-16)+BitShift($iGreen1,-8)+$iBlue1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ColorBrighten
; Description....: Returns brightened or darkened color
; Syntax.........: _ColorBrighten($iColor, $iColorDelta)
; Parameters.....: $iColor					- Color to brighten and darken
;                  $iColorDelta				- Delta to add, minus value is substract
; Return values..: Brightened or darkened color
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ColorBrighten($iColor,$iColorDelta)
	Local $iRed = BitAND(BitShift($iColor,16),0xFF)+$iColorDelta,$iGreen = BitAND(BitShift($iColor,8),0xFF)+$iColorDelta,$iBlue = BitAnd($iColor,0xFF)+$iColorDelta
	If $iRed > 255 Then
		$iRed = 255
	ElseIf $iRed < 0 Then
		$iRed = 0
	EndIf
	If $iGreen > 255 Then
		$iGreen = 255
	ElseIf $iGreen < 0 Then
		$iGreen = 0
	EndIf
	If $iBlue > 255 Then
		$iBlue = 255
	ElseIf $iBlue < 0 Then
		$iBlue = 0
	EndIf
	Return BitShift($iRed,-16)+BitShift($iGreen,-8)+$iBlue
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ColorBGRtoRGB
; Description....: Converts BGR color to RGB color or vice versa
; Syntax.........: _ColorBGRtoRGB($iColor)
; Parameters.....: $iColor					- BGR color to convert
; Return values..: RGB color
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ColorBGRtoRGB($iColor)
	Return BitShift(BitAND($iColor,0x0000FF),-16)+BitAND($iColor,0x00FF00)+BitShift(BitAND($iColor,0xFF0000),16)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlGetType
; Description....: Gets the type of a GUI control
; Syntax.........: _GUICtrlGetType( $hControl [, $bTypeID = True [, $bDetail = False ]])
; Parameters.....: $hControl				- GUI control handle
;                  $bTypeID					- If true return type id ($GUI_TYPE_*)
;                  $bDetail					- If true return detailed control name/id like $GUI_TYPE_PUSHBUTTON instead of $GUI_TYPE_BUTTON
; Remark.........: $GUI_TYPE_PUSHBUTTON indicates a colored button in AutoIt.
; @error.........: 0 = No error, 1 = Not a handle or a control, 2 = Couldn't retrieve control class, 3 = Couldn't retrieve control style, 4 = Control class is unknown
; Return values..: String or ID of control type. At failure empty string or $GUI_TYPE_FAILURE
;                  Unknown control type		"" or $GUI_TYPE_UNKNOWN
;                  Label					"Label" or $GUI_TYPE_LABEL
;                  Picture					"Pic" or $GUI_TYPE_PIC
;                  Icon						"Icon" or $GUI_TYPE_ICON
;                  Button					"Button" or $GUI_TYPE_BUTTON
;                  Checkbox					"Checkbox" or $GUI_TYPE_CHECKBOX
;                  Radio					"Radio" or $GUI_TYPE_RADIO
;                  Group					"Group" or $GUI_TYPE_GROUP
;                  Pushbox					"Pushbutton" or $GUI_TYPE_PUSHBUTTON (these 2 only if $bDetail = True)
;                  List						"List" or $GUI_TYPE_LISTBOX
;                  Combo					"Combo" or $GUI_TYPE_COMBOBOX
;                  ListView					"ListView" or $GUI_TYPE_LISTVIEW
;                  Edit						"Edit" or $GUI_TYPE_EDIT
;                  Input					"Input" or $GUI_TYPE_INPUT
;                  Slider					"Slider" or $GUI_TYPE_SLIDER
;                  Progress					"Progress" or $GUI_TYPE_PROGRESSBAR
;                  Tab						"Tab" or $GUI_TYPE_TAB
;                  TreeView					"TreeView" or $GUI_TYPE_TREEVIEW
;                  Date						"Date" or $GUI_TYPE_DATETIME
;                  Month calendar			"MonthCal" or $GUI_TYPE_CALENDAR
;                  ComboBoxEx				"ComboBoxEx" or $GUI_TYPE_COMBOBOXEX
;                  Animation				"AVI" or $GUI_TYPE_ANIMATION
;                  Header					"Header" or $GUI_TYPE_HEADER
;                  IP address				"IpAddress" or $GUI_TYPE_IPADDRESS
;                  Rebar					"Rebar" or $GUI_TYPE_REBAR
;                  Toolbar					"Toolbar" or $GUI_TYPE_TOOLBAR
;                  Statusbar				"Statusbar" or $GUI_TYPE_STATUSBAR
;                  Tooltip					"ToolTip" or $GUI_TYPE_TOOLTIP
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlGetType($hControl,$bTypeID = True,$bDetail = False)
	If IsHWnd($hControl) = 0 Then
		$hControl = GUICtrlGetHandle($hControl)
		If IsHWnd($hControl) = 0 Then Return SetError(1,0,$bTypeID ? -1 : "")
	EndIf
	Local $sClass = _WinAPI_GetClassName($hControl)
	If $sClass = "" Then Return SetError(2,0,$bTypeID ? -1 : "")				; class retrieval error
	Local $iStyle = _WinAPI_GetWindowLong($hControl,$GWL_STYLE)	; retrieve style
    If @error Then Return SetError(3,0,$bTypeID ? -1 : "")						; style retrieval error
	Switch $sClass
		Case "Static"	; labels, etc.
			If BitAND($iStyle,$SS_BITMAP) = $SS_BITMAP Then Return $bTypeID ? $GUI_TYPE_PIC : "Pic"
			If BitAND($iStyle,$SS_ICON) = $SS_ICON Then Return $bTypeID ? $GUI_TYPE_ICON : "Icon"
			Return $bTypeID ? $GUI_TYPE_LABEL : "Label"
		Case "Button"
			If BitAND($iStyle,$BS_PUSHBOX) = $BS_PUSHBOX Then
				If $bDetail Then
					Return $bTypeID ? $GUI_TYPE_PUSHBUTTON : "Pushbutton"
				Else
					Return $bTypeID ? $GUI_TYPE_BUTTON : "Button"
				EndIf
			EndIf
			If BitAND($iStyle,$BS_CHECKBOX) = $BS_CHECKBOX Or BitAND($iStyle,$BS_AUTOCHECKBOX) = $BS_AUTOCHECKBOX Then Return $bTypeID ? $GUI_TYPE_CHECKBOX : "Checkbox"
			If BitAND($iStyle,$BS_RADIOBUTTON) = $BS_RADIOBUTTON Or BitAND($iStyle,$BS_AUTORADIOBUTTON) = $BS_AUTORADIOBUTTON Then Return $bTypeID ? $GUI_TYPE_RADIO : "Radio"
			If BitAND($iStyle,$BS_GROUPBOX) = $BS_GROUPBOX Then Return $bTypeID ? $GUI_TYPE_GROUP : "Group"
			Return $bTypeID ? $GUI_TYPE_BUTTON : "Button"
		Case "ListBox"
			Return $bTypeID ? $GUI_TYPE_LISTBOX : "List"
		Case "ComboBox"
			Return $bTypeID ? $GUI_TYPE_COMBOBOX : "Combo"
		Case "SysListView32"
			Return $bTypeID ? $GUI_TYPE_LISTVIEW : "ListView"
		Case "Edit"
			If BitAND($iStyle,$ES_MULTILINE) <> $ES_MULTILINE Then Return $bTypeID ? $GUI_TYPE_INPUT : "Input"
			Return $bTypeID ? $GUI_TYPE_EDIT : "Edit"
		Case "msctls_trackbar32"
			Return $bTypeID ? $GUI_TYPE_SLIDER : "Slider"
		Case "msctls_progress32"
			Return $bTypeID ? $GUI_TYPE_PROGRESSBAR : "Progress"
		Case "SysTabControl32"
			Return $bTypeID ? $GUI_TYPE_TAB : "Tab"
		Case "SysTreeView32"
			Return $bTypeID ? $GUI_TYPE_TREEVIEW : "TreeView"
		Case "SysDateTimePick32"
			Return $bTypeID ? $GUI_TYPE_DATETIME : "Date"
		Case "SysMonthCal32"
			Return $bTypeID ? $GUI_TYPE_CALENDAR : "MonthCal"
		Case "ComboBoxEx32"
			Return $bTypeID ? $GUI_TYPE_COMBOBOXEX : "ComboBoxEx"
		Case "SysAnimate32"
			Return $bTypeID ? $GUI_TYPE_ANIMATION : "AVI"
		Case "SysHeader32"
			Return $bTypeID ? $GUI_TYPE_HEADER : "Header"
		Case "SysIPAddress32"
			Return $bTypeID ? $GUI_TYPE_IPADDRESS : "IpAddress"
		Case "ReBarWindow32"
			Return $bTypeID ? $GUI_TYPE_REBAR : "Rebar"
		Case "ToolbarWindow32"
			Return $bTypeID ? $GUI_TYPE_TOOLBAR : "Toolbar"
		Case "msctls_statusbar32"
			Return $bTypeID ? $GUI_TYPE_STATUSBAR : "StatusBar"
		Case "tooltips_class32"
			Return $bTypeID ? $GUI_TYPE_TOOLTIP : "ToolTip"
	EndSwitch
	If $sClass = "" Then SetError(4,0,$bTypeID ? $GUI_TYPE_UNKNOWN : "")	; unknown class
    Return $bTypeID ? $GUI_TYPE_UNKNOWN : $sClass
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlEmpty
; Description....: Tests if edit or input control is empty after stripping spaces, tabs, line feeds or carriage returns
; Syntax.........: _GUICtrlEmpty( $hControl )
; Parameters.....: $hControl				- GUI control handle
; Return values..: True = control is empty, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlEmpty($hControl)
	Return StringLen(StringReplace(StringReplace(StringReplace(StringStripWS(GUICtrlRead($hControl),$STR_STRIPALL),@CR,""),@LF,""),@TAB,"")) == 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlTrimSpaces()
; Description....: Trims leading and trailing spaces from a edit or input control
; Syntax.........: _GUICtrlTrimSpaces( $hControl )
; Parameters.....: $hControl				- GUI control handle
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlTrimSpaces($hControl)
	GUICtrlSetData($hControl,StringStripWS(GUICtrlRead($hControl),$STR_STRIPLEADING+$STR_STRIPTRAILING))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlIsState
; Description....: Checks if all states of given control if one state is present
; Syntax.........: _GUICtrlIsState( $hControl, $iState )
; Parameters.....: $hControl				- GUI control handle
;                  $iState					- GUI control state to check
; Return values..: True = Control has state, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlIsState($hControl,$iState)
	Return BitAnd($iState,GUICtrlGetState($hControl)) > 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlChangeState
; Description....: Changes control to given state if hasn't been set yet, preventing a refresh (flickering)
; Syntax.........: _GUICtrlChangeState( $hControl, $iState )
; Parameters.....: $hControl				- GUI control handle
;                  $iState					- GUI control state to change to if not set
; Return values..: True = Control has been changed, False = Change not needed
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlChangeState($hControl,$iState)
	If BitAnd($iState,GUICtrlGetState($hControl)) = 0 Then
		GUICtrlSetState($hControl,$iState)
		Return True
	EndIf
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLeft
; Description....: Gets left position of given control in given window
; Syntax.........: _GUICtrlLeft( $hGUI, $hControl )
; Parameters.....: $hGUI					- Window GUI handle
;                  $hControl				- GUI control handle
; Return values..: Left position of control in window
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLeft($hGUI,$hControl)
	Local $Positions = ControlGetPos($hGUI,"",$hControl)
	Return $Positions[0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlRight
; Description....: Gets right position of given control in given window
; Syntax.........: _GUICtrlRight( $hGUI, $hControl )
; Parameters.....: $hGUI					- Window GUI handle
;                  $hControl				- GUI control handle
; Return values..: Right position of control in window
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlRight($hGUI,$hControl)
	Local $Positions = ControlGetPos($hGUI,"",$hControl)
	Return $Positions[0]+$Positions[2]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlTop
; Description....: Gets top position of given control in given window
; Syntax.........: _GUICtrlTop( $hGUI, $hControl )
; Parameters.....: $hGUI					- Window GUI handle
;                  $hControl				- GUI control handle
; Return values..: Top position of control in window
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlTop($hGUI,$hControl)
	Local $Positions = ControlGetPos($hGUI,"",$hControl)
	Return $Positions[1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlBottom
; Description....: Gets bottom position of given control in given window
; Syntax.........: _GUICtrlBottom( $hGUI, $hControl )
; Parameters.....: $hGUI					- Window GUI handle
;                  $hControl				- GUI control handle
; Return values..: Bottom position of control in window
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlBottom($hGUI,$hControl)
	Local $Positions = ControlGetPos($hGUI,"",$hControl)
	Return $Positions[1]+$Positions[3]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlWidth
; Description....: Gets width of given control in given window
; Syntax.........: _GUICtrlWidth( $hGUI, $hControl )
; Parameters.....: $hGUI					- Window GUI handle
;                  $hControl				- GUI control handle
; Return values..: Width of control in window
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlWidth($hGUI,$hControl)
	Local $Positions = ControlGetPos($hGUI,"",$hControl)
	Return $Positions[2]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlHeight
; Description....: Gets height of given control in given window
; Syntax.........: _GUICtrlHeight( $hGUI, $hControl )
; Parameters.....: $hGUI					- Window GUI handle
;                  $hControl				- GUI control handle
; Return values..: Height of control in window
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlHeight($hGUI,$hControl)
	Local $Positions = ControlGetPos($hGUI,"",$hControl)
	Return $Positions[3]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlComboBox_AddArray
; Description....: Fills combobox control with array
; Syntax.........: _GUICtrlComboBox_AddArray($hComboBox, $aData [,$iColumn = 0])
; Parameters.....: $hComboBox				- Control handle of combobox
;                  $aData					- Array with values for combobox
;                  $iColumn					- Column if array has 2 dimensions
; Return values..: _GUICtrlComboBox_GetCount
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlComboBox_AddArray($hComboBox, ByRef $aData,$iColumn = 0)
	_GUICtrlComboBox_BeginUpdate($hComboBox)
	For $iRow = 0 To UBound($aData)-1
		If UBound($aData,$UBOUND_DIMENSIONS) = 1 Then
			_GUICtrlComboBox_AddString($hComboBox,$aData[$iRow])
		Else
			_GUICtrlComboBox_AddString($hComboBox,$aData[$iRow][$iColumn])
		EndIf
	Next
	_GUICtrlComboBox_EndUpdate($hComboBox)
	_GUICtrlComboBox_SetCurSel($hComboBox,0)
	Return _GUICtrlComboBox_GetCount($hComboBox)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlList_AddArray
; Description....: Fills listbox control with array
; Syntax.........: _GUICtrlList_AddArray($hComboBox, $aData [,$iColumn = 0])
; Parameters.....: $hComboBox				- Control handle of combobox
;                  $aData					- Array with values for combobox
;                  $iColumn					- Column if array has 2 dimensions
; Return values..: _GUICtrlListBox_GetCount
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlListBox_AddArray($hListBox, ByRef $aData,$iColumn = 0)
	_GUICtrlListBox_BeginUpdate($hListBox)
	For $iRow = 0 To UBound($aData)-1
		If UBound($aData,$UBOUND_DIMENSIONS) = 1 Then
			_GUICtrlListBox_AddString($hListBox,$aData[$iRow])
		Else
			_GUICtrlListBox_AddString($hListBox,$aData[$iRow][$iColumn])
		EndIf
	Next
	_GUICtrlListBox_EndUpdate($hListBox)
	_GUICtrlListBox_SetCurSel($hListBox,0)
	Return _GUICtrlListBox_GetCount($hListBox)
EndFunc

Func _GUICtrlListBox_FindInTextEx($hListBox,$sText,$iStart = -1,$bWrapOK = True,$bReverse = False)
	If $bReverse Then
		Local $iItems = _GUICtrlListBox_GetCount($hListBox)
		If $iItems = -1 Then Return -1

		If $iStart = -1 Or ($bWrapOK And $iStart = 0) Then
		consolewrite($iItems-1 & " " & $iStart)
			For $iItem = $iItems-1 To 0 Step -1
				If StringInStr(_GUICtrlListBox_GetText($hListBox,$iItem),$sText) Then Return $iItem
			Next
		consolewrite("-1")
			Return -1
		EndIf
		For $iItem = $iStart-1 To 0 Step -1
			If StringInStr(_GUICtrlListBox_GetText($hListBox,$iItem),$sText) Then Return $iItem
		Next
		If Not $bWrapOK Then Return -1
		For $iItem = $iItems-1 To $iStart Step -1
			If StringInStr(_GUICtrlListBox_GetText($hListBox,$iItem),$sText) Then Return $iItem
		Next
		Return -1
	Else
		_GUICtrlListBox_FindInText($hListBox,$sText,$iStart,$bWrapOK)
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLinkLabel_Create
; Description....: Creates a hyperlink label
; Syntax.........: _GUICtrlLinkLabel_Create($sText, $iTop, $iLeft [,$iWidth = -1 [,$iHeight = -1 [,$iStyle = -1, [$iExStyle = -1]]]])
; Parameters.....: $sText					- Label text
;                  $iTop					- Label top position
;                  $iLeft					- label left position
;                  $iWidth					- Label width
;                  $iHeight					- Label height
;                  $iStyle					- Label style
;                  $iExStyle				- Extended label style
; Return values..: None
; Return handle value				Link label control handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLinkLabel_Create($sText,$iTop,$iLeft,$iWidth = -1,$iHeight = -1,$iStyle = -1,$iExStyle = -1)
	Local $hLabel = GUICtrlCreateLabel($sText,$iTop,$iLeft,$iWidth,$iHeight,$iStyle,$iExStyle),$iColor,$iFontSize
	If IsDeclared("_iLinkLabelColor") = $DECLARED_GLOBAL Then
		$iColor = Eval("_iLinkLabelColor")
	Else
		$iColor = $COLOR_BLUE
	EndIf
	If IsDeclared("_iLinkLabelFontSize") = $DECLARED_GLOBAL Then
		$iFontSize = Eval("_iLinkLabelFontSize")
	Else
		$iFontSize = 8.5
	EndIf
	GUICtrlSetColor(-1,$iColor)
	GUICtrlSetFont(-1,$iFontSize,$FW_NORMAL,$GUI_FONTUNDER)
	GUICtrlSetCursor(-1,0)			; hand mouse pointer
	Return $hLabel
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLinkLabel_Clicked
; Description....: Opens given URL and shows user has clicked link
; Syntax.........: _GUICtrlLinkLabel_Clicked($hLabel, $sURL [,$iColor = -1])
; Parameters.....: $hLabel					- Label GUI handle
;                  $sURL					- URL to open
;                  $iColor					- Label color, -1 = _GUICtrlLinkLabel_SetClickedColor() or default $COLOR_PURPLE
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLinkLabel_Clicked($hLabel,$sURL,$iColor = -1)
	If $iColor = -1 And IsDeclared("_iLinkLabelLinkedColor") = $DECLARED_GLOBAL Then
		$iColor = Eval("_iLinkLabelLinkedColor")
	Else
		$iColor = $COLOR_PURPLE
	EndIf
	GUICtrlSetColor($hLabel,$iColor)
	ShellExecute($sURL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLinkLabel_SetColor
; Description....: Sets color of unopened (not clicked yet) labels
; Syntax.........: _GUICtrlLinkLabel_SetColor([$iColor = $COLOR_BLUE])
; Parameters.....: $iColor					- Label color
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLinkLabel_SetColor($iColor = $COLOR_BLUE)
	Assign("_iLinkLabelColor",$iColor,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLinkLabel_SetClickedColor
; Description....: Sets color of clicked/opened URL labels
; Description....: Sets color of unopened (not clicked yet) labels
; Syntax.........: _GUICtrlLinkLabel_SetClickedColor([$iColor = $COLOR_PURPLE])
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLinkLabel_SetClickedColor($iColor = $COLOR_PURPLE)
	Assign("_iLinkLabelLinkedColor",$iColor,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLinkLabel_SetFontSize
; Description....: Sets font size of link labels
; Syntax.........: _GUICtrlLinkLabel_SetFontSize([$iFontSize = 8.5])
; Parameters.....: $iFontSize				- Label font size
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLinkLabel_SetFontSize($iFontSize = 8.5)
	Assign("_iLinkLabelFontSize",$iFontSize,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlLinkLabel_GetFontSize
; Description....: Gets font size of link labels
; Syntax.........: _GUICtrlLinkLabel_GetFontSize()
; Parameters.....: None
; Return values..: Link label font size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlLinkLabel_GetFontSize()
	If IsDeclared("_iLinkLabelFontSize") = $DECLARED_GLOBAL Then
		Return Eval("_iLinkLabelFontSize")
	Else
		Return 8.5
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtons
; Description....: Initializes graphic buttons (buttons with a png or jpg image) or cleans up resources
; Syntax.........: _GraphicButtons([$bInitialize = True])
; Parameters.....: $bInitialize				- True = Initialize graphic buttons, False = Clean up resources
;                  $bGetBackgroundColor		- True = Default window background color of Windows, False = Transparent
; Return values..: None
; Remarks........: GDI+ must be initialized (see _GDIPlus_Startup())
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtons($bInitialize = True,$bGetBackgroundColor = False)
	If $bInitialize Then
		Global $_bGraphicButtonsInitialized = False,$_aGraphicButtons[1],$_iGraphicButtonsBackgroundColor = 0xFF000000	; transparent
		If $bGetBackgroundColor Then $_iGraphicButtonsBackgroundColor = _WindowGetBkColor()
	ElseIf $_bGraphicButtonsInitialized Then
		For $iButton = 0 To UBound($_aGraphicButtons)-1
			_GDIPlus_ImageDispose($_aGraphicButtons[$iButton][1])
			_WinAPI_DeleteObject($_aGraphicButtons[$iButton][2])
		Next
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtonsSetBkColor
; Description....: Sets background color of window for conversion of png/jpg to bitmap
; Syntax.........: _GraphicButtonsSetBkColor($iColor)
; Parameters.....: $iColor					- Background color of window used for image bitmap conversion
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtonsSetBkColor($iColor = 0xFF000000)
	$_iGraphicButtonsBackgroundColor = $iColor
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtonsCount
; Description....: Returns number of graphic buttons
; Syntax.........: _GraphicButtonsCount()
; Parameters.....: None
; Return values..: Number of graphic buttons
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtonsCount()
	If Not $_bGraphicButtonsInitialized Then Return 0
	Return UBound($_aGraphicButtons)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButton
; Description....: Creates graphic button or graphic image
; Syntax.........: _GraphicButton($sGraphicFile, $sText, $sToolTip, $iLeft, $iTop [,$iWidth = -1 [,$iHeight = -1 [,$iStyle = -1 [,$iExStyle = -1]]]])
; Parameters.....: $sGraphicFile			- File path + name containing png/jpg image
;                  $sText					- Text of button, text = 0 then image is shown without a button
;                  $sToolTip				- Text of tooltip, if empty no tooltip is made
;                  $iLeft					- Left position
;                  $iTop					- Top position
;                  $iWidth = -1				- Width of button, if image control width is loaded image width
;                  $iHeight = -1			- Height of button, if image control height is loaded image height
;                  $iStyle = -1				- Button or image style
;                  $iExStyle = -1			- Button or image extended style
; Return values..: Button control handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButton($sGraphicFile,$sText,$sToolTip,$iLeft,$iTop,$iWidth = -1,$iHeight = -1,$iStyle = -1,$iExStyle = -1)
	If Not $_bGraphicButtonsInitialized Then
		$_bGraphicButtonsInitialized = True
		ReDim $_aGraphicButtons[1][7]
	Else
		ReDim $_aGraphicButtons[UBound($_aGraphicButtons)+1][7]
	EndIf
	If IsString($sText) Then
		If StringLen($sText) > 0 Then
			$sText = " " & $sText
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][0] = GUICtrlCreateButton($sText,$iLeft,$iTop,$iWidth,$iHeight,$iStyle,$iExStyle)
		ElseIf $iStyle = -1 Then
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][0] = GUICtrlCreateButton("",$iLeft,$iTop,$iWidth,$iHeight,$BS_BITMAP,$iExStyle)
		Else
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][0] = GUICtrlCreateButton("",$iLeft,$iTop,$iWidth,$iHeight,$iStyle+$BS_BITMAP,$iExStyle)
		EndIf
		$_aGraphicButtons[UBound($_aGraphicButtons)-1][3] = $iWidth
		$_aGraphicButtons[UBound($_aGraphicButtons)-1][4] = $iHeight
	Else
		$_aGraphicButtons[UBound($_aGraphicButtons)-1][0] = -1
		$_aGraphicButtons[UBound($_aGraphicButtons)-1][3] = 0
		$_aGraphicButtons[UBound($_aGraphicButtons)-1][4] = 0
	EndIf
	$_aGraphicButtons[UBound($_aGraphicButtons)-1][5] = $sText
	$_aGraphicButtons[UBound($_aGraphicButtons)-1][6] = $sToolTip
	If FileExists($sGraphicFile) Then
		$_aGraphicButtons[UBound($_aGraphicButtons)-1][1] = _GDIPlus_ImageLoadFromFile($sGraphicFile)
		If IsString($sText) Then
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][2] = _GDIPlus_BitmapCreateHBITMAPFromBitmap($_aGraphicButtons[UBound($_aGraphicButtons)-1][1],$_iGraphicButtonsBackgroundColor)
			_WinAPI_DeleteObject(GUICtrlSendMsg($_aGraphicButtons[UBound($_aGraphicButtons)-1][0],$BM_SETIMAGE,$IMAGE_BITMAP,$_aGraphicButtons[UBound($_aGraphicButtons)-1][2]))
		Else
			If $iWidth = -1 Then $iWidth = _GDIPlus_ImageGetWidth($_aGraphicButtons[UBound($_aGraphicButtons)-1][1])
			If $iHeight = -1 Then $iHeight = _GDIPlus_ImageGetHeight($_aGraphicButtons[UBound($_aGraphicButtons)-1][1])
			$hImage = _GDIPlus_ImageResize($_aGraphicButtons[UBound($_aGraphicButtons)-1][1],$iWidth,$iHeight)
			_GDIPlus_ImageDispose($_aGraphicButtons[UBound($_aGraphicButtons)-1][1])
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][1] = $hImage
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][0] = GUICtrlCreatePic("",$iLeft,$iTop,$iWidth,$iHeight,$iStyle,$iExStyle)
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][2] = _GDIPlus_BitmapCreateHBITMAPFromBitmap($_aGraphicButtons[UBound($_aGraphicButtons)-1][1],$_iGraphicButtonsBackgroundColor)
			_WinAPI_DeleteObject(GUICtrlSendMsg($_aGraphicButtons[UBound($_aGraphicButtons)-1][0],$STM_SETIMAGE,$IMAGE_BITMAP,$_aGraphicButtons[UBound($_aGraphicButtons)-1][2]))
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][3] = $iWidth
			$_aGraphicButtons[UBound($_aGraphicButtons)-1][4] = $iHeight
		EndIf
		If StringLen($sToolTip) > 0 Then GUICtrlSetTip($_aGraphicButtons[UBound($_aGraphicButtons)-1][0],$sToolTip)
	EndIf
	Return $_aGraphicButtons[UBound($_aGraphicButtons)-1][0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtonWidth
; Description....: Returns graphic button width or image width
; Syntax.........: _GraphicButtonWidth($hGraphicButton)
; Parameters.....: $hGraphicButton			- Handle of graphic button
; Return values..: Graphic button width or image width
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtonWidth($hGraphicButton)
	If Not $_bGraphicButtonsInitialized Or $hGraphicButton = -1 Then Return 0
	For $iButton = 0 To UBound($_aGraphicButtons)-1
		If $_aGraphicButtons[$iButton][0] = $hGraphicButton Then Return $_aGraphicButtons[$iButton][3]
	Next
	Return 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtonHeight
; Description....: Returns graphic button height or image height
; Syntax.........: _GraphicButtonHeight($hGraphicButton)
; Parameters.....: $hGraphicButton			- Handle of graphic button
; Return values..: Graphic button height or image height
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtonHeight($hGraphicButton)
	If Not $_bGraphicButtonsInitialized Or $hGraphicButton = -1 Then Return 0
	For $iButton = 0 To UBound($_aGraphicButtons)-1
		If $_aGraphicButtons[$iButton][0] = $hGraphicButton Then Return $_aGraphicButtons[$iButton][4]
	Next
	Return 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtonText
; Description....: Returns text on graphic button
; Syntax.........: _GraphicButtonText($hGraphicButton)
; Parameters.....: $hGraphicButton			- Handle of graphic button
; Return values..: Button text
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtonText($hGraphicButton)
	If Not $_bGraphicButtonsInitialized Or $hGraphicButton = -1 Then Return ""
	For $iButton = 0 To UBound($_aGraphicButtons)-1
		If $_aGraphicButtons[$iButton][0] = $hGraphicButton Then Return $_aGraphicButtons[$iButton][5]
	Next
	Return ""
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GraphicButtonTooltip
; Description....: Returns tooltip of graphic button
; Syntax.........: _GraphicButtonTooltip($hGraphicButton)
; Parameters.....: $hGraphicButton			- Handle of graphic button
; Return values..: Tooltip of graphic button
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GraphicButtonTooltip($hGraphicButton)
	If Not $_bGraphicButtonsInitialized Or $hGraphicButton = -1 Then Return ""
	For $iButton = 0 To UBound($_aGraphicButtons)-1
		If $_aGraphicButtons[$iButton][0] = $hGraphicButton Then Return $_aGraphicButtons[$iButton][6]
	Next
	Return ""
EndFunc