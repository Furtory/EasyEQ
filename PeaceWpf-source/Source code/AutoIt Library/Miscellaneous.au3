#include-once
#include <AutoItConstants.au3>
#include <FileConstants.au3>
#include <WinAPILocale.au3>
#include <Timers.au3>
#include <String.au3>
#include <WinAPIProc.au3>
; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.30
; Description....: Miscellaneous Functions
; Author(s)......: Peter Verbeek
; Version history: 1.30		added: _WindowsVersionWorld() reads Windows version from registry as presented to the world
;                       	added: _ProcessChildProcesses() gets child processes of given process (alternative for _WinAPI_EnumChildProcess(), using AutoIt ProcessList() and _WinAPI_GetParentProcess())
;                  	    	added: _ProcessFirstChildProcess() gets first child process of given process
;                  	    	improved: _RegistryDelete() can also delete an entire key
;                  	    	bug: _RegistryWrite() didn't had a key type argument as described in RegWrite()
;                  1.29		added: _VersionHigher() checks which of 2 given version numbers is higher
;                  1.26		improved: _WindowsVersion() correctly returns the version for Windows 10 and 11 (by reading the new key value)
;                       	added: _RegistryDelete() and _RegistryWrite() for deleting and writing a registry key item taking in account a 64 bit Windows
;                      		improved: _RegistryRead() now returns @error and @extended as the RegRead() function
;                  1.22		added: _WindowsScrollInactiveWindow() to read Windows setting to scroll an inactive window
;                  1.19		added: _ProcessRunsAlready() returns if a process is already running when the same process is started again (singleton), process name may include path, when no processes given @ScriptName is used
;                  			added: _ProcessInstances() gets number of instances of a process or processes, process name may include path, when no processes given @ScriptName is used
;                  			improved: _ProcessGetProcessId(), process name may include path
;                  			added: _CHMShow() to show CHM file (having manual, help info, etc.) located in @ScriptDir, file may have or not have the .chm extension
;                  			added: _CHMShowOnF1() to show CHM file (having manual, help info, etc.) located in @ScriptDir after pressing F1 on a GUI, file may have or not have the .chm extension
;                  1.18		added: _SoundGetWaveVolume() to get app volume of script, Windows Vista, 7, 8, 10 only
;                  1.17		added: _WindowsBuildNumber() and _WindowsUBRNumber() to read build and update build release numbers. Can be used to determine if Windows has been updated
;                  1.16		added: _FileGetSizeTimed() tries to get file size in a loop when FileGetSize() fails (when Windows isn't done yet) for instance just after downloading the file
;                  1.14		added: _ProcessGetProcessId() to get process id by name
;                  1.00		initial version
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;	_Console
;	_LogSwitch
;	_LogIsOn
;	_LogFile
;	_LogWrite
;	_LogDelete
;	_LogRead
;	_CHMShow
;	_CHMShowOnF1
;	_FileGetSizeTimed
;	_ProcessGetProcessId
;	_ProcessInstances
;	_ProcessRunsAlready
;	_ProcessChildProcesses
;	_ProcessFirstChildProcess
;	_RegistryRead
;	_RegistryWrite
;	_RegistryDelete
;	_VersionHigher
;	_WindowsVersionWorld
;	_WindowsVersion
;	_WindowsBuildNumber
;	_WindowsUBRNumber
;	_WindowsScrollInactiveWindow
;	_LocaleDecimal
;	_LocaleThousand
;	_SoundGetWaveVolume
; ===============================================================================================================================

; List of 27 functions
;	_Console						Debugging				Writes text or array of text to console with @CRLF
;	_LogSwitch						Debugging				Switches logging on or off
;	_LogIsOn						Debugging				Returns if logging is switched on
;	_LogFile						Debugging				Sets log file
;	_LogWrite						Debugging				Writes event to log file if logging is on
;	_LogDelete						Debugging				Deletes a log file
;	_LogRead						Debugging				Reads the content of a log file
;	_CHMShow						Help / Manual			Shows CHM file (having manual, help info, etc.) located in @ScriptDir, file may have or not have the .chm extension
;	_CHMShowOnF1					Help / Manual			Shows CHM file (having manual, help info, etc.) located in @ScriptDir after pressing F1 on a GUI, file may have or not have the .chm extension
;	_FileGetSizeTimed				File					Tries to get file size in a loop when FileGetSize() fails (when Windows isn't done yet) for instance just after downloading the file
;	_ProcessGetProcessId			Process information		Gets process id of given process name
;	_ProcessInstances				Process information		Gets number of instances of a process or processes, process name may include path
;	_ProcessRunsAlready				Process information		Returns if a process is already running when the same process is started again (singleton), process name may include path
;	_ProcessChildProcesses			Process information		Gets child processes of given process (alternative for _WinAPI_EnumChildProcess(), using AutoIt ProcessList() and _WinAPI_GetParentProcess())
;	_ProcessFirstChildProcess		Process information		Gets first child process of given process
;	_RegistryRead					System information		Reads a key value of the registry taking in account a 64 bit Windows
;															Remark: For reading some key values of the registry administrator rights could be needed
;	_RegistryWrite					System information		Writes a key value to the registry taking in account a 64 bit Windows
;															Remark: For writing some key values to the registry administrator rights could be needed
;	_RegistryDelete					System information		Deletes a key value or key from the registry taking in account a 64 bit Windows
;															Remark: For deleting some keys from the registry administrator rights could be needed
;	_VersionHigher					General information		Checks which of the 2 given version numbers is higher
;	_WindowsVersionWorld			System information		Reads Windows version from registry as presented to the world
;															Remark: Only for use on Windows 7 and higher
;	_WindowsVersion					System information		Reads Windows version from registry
;															Remark: Windows 11 is registered as being Windows 10!
;	_WindowsBuildNumber				System information		Reads Windows build number from registry
;	_WindowsUBRNumber				System information		Reads Windows update build release number from registry
;	_WindowsScrollInactiveWindow	System information		Reads Windows setting to scroll an inactive window
;	_LocaleDecimal					System information		Returns decimal sign on local computer
;	_LocaleThousand					System information		Returns thousands sign on local computer
;	_SoundGetWaveVolume				System information		Returns app volume of script, Windows Vista, 7, 8, 10 only

; #FUNCTION# ====================================================================================================================
; Name...........: _Console
; Description....: Writes text or array of text to console with @CRLF
; Syntax.........: _Console($vText1 [,$vText2 = Default [,$vText3 = Default [,$vText4 = Default [,$vText5 = Default [,$vText6 = Default [,$vText7 = Default [,$vText8 = Default [,$vText9 = Default [,$vText10 = Default]]]]]]]]])
; Parameters.....: $vText1					- Text or array to write to console
;                  $vText2					- Text to write or boolean: True = Text on new line, False = Not
;                  $vText3 to 10			- Text 3 to 10 to write to console
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Console($vText1,$vText2 = Default,$vText3 = Default,$vText4 = Default,$vText5 = Default,$vText6 = Default,$vText7 = Default,$vText8 = Default,$vText9 = Default,$vText10 = Default)
	If IsArray($vText1) Then
		If UBound($vText1) > 0 Then
			For $nRow = 0 To UBound($vText1)-1
				If UBound($vText1,$UBOUND_COLUMNS) = 0 Then
					ConsoleWrite($vText1[$nRow])
				Else
					For $nColumn = 0 To UBound($vText1,$UBOUND_COLUMNS)-1
						ConsoleWrite($vText1[$nRow][$nColumn] & " ")
					Next
				EndIf
				If $vText2 = Default Or (IsBool($vText2) And $vText2) Then ConsoleWrite(@CRLF)
			Next
		EndIf
	Else
		If $vText2 = Default Then
			ConsoleWrite($vText1 & @CRLF)
		ElseIf $vText3 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF)
		ElseIf $vText4 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF)
		ElseIf $vText5 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF)
		ElseIf $vText6 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF & $vText5 & @CRLF)
		ElseIf $vText7 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF & $vText5 & @CRLF & $vText6 & @CRLF)
		ElseIf $vText8 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF & $vText5 & @CRLF & $vText6 & @CRLF & $vText7 & @CRLF)
		ElseIf $vText9 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF & $vText5 & @CRLF & $vText6 & @CRLF & $vText7 & @CRLF & $vText8 & @CRLF)
		ElseIf $vText10 = Default Then
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF & $vText5 & @CRLF & $vText6 & @CRLF & $vText7 & @CRLF & $vText8 & @CRLF & $vText9 & @CRLF)
		Else
			ConsoleWrite($vText1 & @CRLF & $vText2 & @CRLF & $vText3 & @CRLF & $vText4 & @CRLF & $vText5 & @CRLF & $vText6 & @CRLF & $vText7 & @CRLF & $vText8 & @CRLF & $vText9 & @CRLF & $vText10 & @CRLF)
		EndIf
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogSwitch
; Description....: Switches logging on or off
; Syntax.........: _LogSwitch([$bLog = True])
; Parameters.....: $bLog = True				- True = Switch logging on, False = Switch off
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogSwitch($bLog = True)
	Assign("_bLogSwitch",$bLog,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogIsOn
; Description....: Returns if logging is switched on
; Syntax.........: _LogIsOn()
; Parameters.....: None
; Return values..: True = Logging is on, False = Off
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogIsOn()
	If IsDeclared("_bLogSwitch") <> $DECLARED_GLOBAL Or Not Eval("_bLogSwitch") Then Return False
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogFile
; Description....: Sets log file
; Syntax.........: _LogFile($sLogFile)
; Parameters.....: $sLogFile				- File for logging info
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogFile($sLogFile = @MyDocumentsDir & "\Log.txt")
	If StringLen($sLogFile) = 0 Then $sLogFile = @MyDocumentsDir & "\Log.txt"
	Assign("_sLogFile",$sLogFile,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogWrite
; Description....: Writes event to log file if logging is on
; Syntax.........: _LogWrite($sText [,$sLogFile = ""])
; Parameters.....: $sText					- Event to log in format: current date time $sText
;                  $sLogFile				- Log file to write to, if empty then file set by _LogFile is used
; Return values..: True = Event has been written, False = Logging is off or couldn't open log file
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogWrite($sText,$sLogFile = "")
	If IsDeclared("_bLogSwitch") <> $DECLARED_GLOBAL Or Not Eval("_bLogSwitch") Then Return False	; logging is off, no writing
	If IsDeclared("_sLogFile") = $DECLARED_GLOBAL And StringLen($sLogFile) = 0 Then $sLogFile = Eval("_sLogFile")
	If StringLen($sLogFile) = 0 Then $sLogFile = @MyDocumentsDir & "\Log.txt"
	Local $hFileHandle = FileOpen($sLogFile,$FO_APPEND)
	If $hFileHandle = -1 Then Return False
	FileWrite($hFileHandle,@YEAR & "-" & @MON & "-" & @MDAY & " " & @HOUR & ":" & @MIN & ":" & @SEC & ":" & @MSEC & " " & $sText & @CRLF)
	FileClose($hFileHandle)
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogDelete
; Description....: Deletes a log file
; Syntax.........: _LogDelete([$sLogFile = ""])
; Parameters.....: $sLogFile				- Log file to delete, if empty then file set by _LogFile is used
; Return values..: True = File is deleted, False = Logging wasn't switched on during program run or file doesn't exist
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogDelete($sLogFile = "")
	If IsDeclared("_sLogFile") = $DECLARED_GLOBAL And StringLen($sLogFile) = 0 Then $sLogFile = Eval("_sLogFile")
	If StringLen($sLogFile) = 0 Then $sLogFile = @MyDocumentsDir & "\Log.txt"
	If FileExists($sLogFile) Then
		FileDelete($sLogFile)
		Return True
	EndIf
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogRead
; Description....: Reads the content of a log file
; Syntax.........: _LogRead([$sLogFile = ""])
; Parameters.....: $sLogFile				- Log file to read, if empty then file set by _LogFile is used
; Return values..: Content of log file, if empty log file is empty or not readable
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogRead($sLogFile = "")
	If IsDeclared("_sLogFile") = $DECLARED_GLOBAL And StringLen($sLogFile) = 0 Then $sLogFile = Eval("_sLogFile")
	If StringLen($sLogFile) = 0 Then $sLogFile = @MyDocumentsDir & "\Log.txt"
	If FileExists($sLogFile) Then
		Return FileRead($sLogFile)
	EndIf
	Return ""
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _CHMShow
; Description....: Shows CHM file (having manual, help info, etc.) located in @ScriptDir, file may have or not have the .chm extension
; Syntax.........: _CHMShow( $cCHMFile [, $cTopic = ""] )
; Parameters.....: $cCHMFile				- CHM (help manual) file
;                  $cTopic					- Topic (section) to show
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _CHMShow($cCHMFile,$cTopic = "")
	ShellExecute(@WindowsDir & "\hh.exe","its:" & @ScriptDir & "\" & $cCHMFile & (StringLower(StringRight($cCHMFile,4)) = ".chm" ? "" : ".chm") & (StringLen($cTopic) = 0 ? "" : "::/" & $cTopic))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _CHMShowOnF1
; Description....: Shows CHM file (having manual, help info, etc.) located in @ScriptDir after pressing F1 on a GUI, file may have or not have the .chm extension
; Syntax.........: _CHMShowOnF1( $cCHMFile [, $cTopic = ""] )
; Parameters.....: $cCHMFile				- CHM (help manual) file
;                  $cTopic					- Topic (section) to show
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _CHMShowOnF1($cCHMFile,$cTopic = "")
	GUISetHelp(@WindowsDir & "\hh.exe its:" & @ScriptDir & "\" & $cCHMFile & (StringLower(StringRight($cCHMFile,4)) = ".chm" ? "" : ".chm") & (StringLen($cTopic) = 0 ? "" : "::/" & $cTopic))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileGetSizeTimed
; Description....: Tries to get file size in a loop when FileGetSize() fails (when Windows isn't done yet) for instance just after downloading the file
; Syntax.........: _FileGetSizeTimed($sFile [, $nTimeout = 5000 [, $nWaitTime = 100]] )
; Parameters.....: $sFile					- File to get size of
;                  $nTimeout				- Timeout in milliseconds, default 5000 ms
;                  $nWaitTime				- Time pause for checking again, default 100 ms
; Return values..: File size or 0 when failed or actually size is 0 (check @error)
; Error values...: @error = 1				- Failed to get size, you might need to increase $Timeout value
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileGetSizeTimed($sFile,$nTimeout = 5000,$nWaitTime = 100)
	Local $nFileSize,$hFileSizeTimer = _Timer_Init(),$nError = 0

	While _Timer_Diff($hFileSizeTimer) < $nTimeout
		$nFileSize = FileGetSize($sFile)
		$nError = @error
		If $nError <> 0 Or $nFileSize = 0 Then	; check on error or 0 size which indicates that the FileGetSize() function is yet able to return correct size
			Sleep($nWaitTime)
			ContinueLoop
		EndIf
		Return $nFileSize
	WEnd
	If $nError = 0 Then
		Return 0
	Else
		SetError(1,0,0)
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProcessGetProcessId
; Description....: Gets process id of given process name, process name may include path
; Syntax.........: _ProcessGetProcessId( $cProcessName )
; Parameters.....: $cProcessName			- Name of process (executable, may include path)
; Return values..: Process Id or 0 if not found or -1 if process list couldn't be built
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProcessGetProcessId($cProcessName)
	Local $aProcessDirs = StringSplit($cProcessName,":\")
	If $aProcessDirs[0] > 0 Then $cProcessName = $aProcessDirs[$aProcessDirs[0]]
	Local $aProcessesList = ProcessList($cProcessName)
	If @error = 1 Then Return -1
	If $aProcessesList[0][0] = 0 Then Return 0
	Return $aProcessesList[1][1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProcessRunsAlready
; Description....: Returns if a process is already running when the same process is started again (singleton), process name may include path, when no processes given @ScriptName is used
; Syntax.........: _ProcessRunsAlready( [$cProcessName1 [,$cProcessName2] [,$cProcessName3]]] )
; Parameters.....: $cProcessName1			- Name of process (executable, may include path)
;                  $cProcessName2			- Equivalent name of process (executable, may include path), for instance 32 or 64 bit version
;                  $cProcessName3			- Equivalent name of process (executable, may include path), for instance 32 or 64 bit version
; Return values..: True if process is already running thus 2 or more times as the current instances is also running for checking
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProcessRunsAlready($cProcessName1 = "",$cProcessName2 = "",$cProcessName3 = "")
	If StringLen($cProcessName1 & $cProcessName2 & $cProcessName3) = 0 Then	Return _ProcessInstances(@ScriptName) > 1		; check for more than 1 @ScriptName process
	; strip paths from process names
	Local $aProcessDirs = StringSplit($cProcessName1,":\")
	If $aProcessDirs[0] > 0 Then $cProcessName1 = $aProcessDirs[$aProcessDirs[0]]
	$aProcessDirs = StringSplit($cProcessName2,":\")
	If $aProcessDirs[0] > 0 Then $cProcessName2 = $aProcessDirs[$aProcessDirs[0]]
	$aProcessDirs = StringSplit($cProcessName3,":\")
	If $aProcessDirs[0] > 0 Then $cProcessName3 = $aProcessDirs[$aProcessDirs[0]]
	; filter out same process names
	If StringLower($cProcessName3) = StringLower($cProcessName2) Then $cProcessName3 = ""
	If StringLower($cProcessName2) = StringLower($cProcessName1) Then $cProcessName2 = ""
	; check if runs twice or more
	If StringLen($cProcessName3) = 0 And StringLen($cProcessName2) = 0 Then Return _ProcessInstances($cProcessName1) > 1	; check for more than 1 $cProcessName1 process
	If StringLen($cProcessName3) = 0 Then Return _ProcessInstances($cProcessName1)+_ProcessInstances($cProcessName2) > 1	; check for more than 1 $cProcessName1 + $cProcessName2 process
	Return _ProcessInstances($cProcessName1)+_ProcessInstances($cProcessName2)+_ProcessInstances($cProcessName3) > 1		; check for more than 1 $cProcessName1 + $cProcessName2 + $cProcessName3 + process
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProcessInstances
; Description....: Gets number of instances of a process or processes, process name may include path, when no processes given @ScriptName is used
; Syntax.........: _ProcessInstances( [$cProcessName1 [,$cProcessName2] [,$cProcessName3] [,$cProcessName4] [,$cProcessName5]]]]] )
; Parameters.....: $cProcessName1			- Name of a process (executable, may include path)
;                  $cProcessName2			- Name of a process (executable, may include path)
;                  $cProcessName3			- Name of a process (executable, may include path)
;                  $cProcessName4			- Name of a process (executable, may include path)
;                  $cProcessName5			- Name of a process (executable, may include path)
; Return value...: Number of instances of a process or processes
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProcessInstances($cProcessName1 = "",$cProcessName2 = "",$cProcessName3 = "",$cProcessName4 = "",$cProcessName5 = "")
	Local $aProcesses = [$cProcessName1,$cProcessName2,$cProcessName3,$cProcessName4,$cProcessName5],$aProcessDirs,$aProcess,$iCountInstances = 0,$cProcessName,$bAlreadyChecked

	For $iProcess = 0 To UBound($aProcesses)-1
		If $iProcess = 0 And StringLen($aProcesses[$iProcess]) = 0 Then $aProcesses[$iProcess] = @ScriptName
		If StringLen($aProcesses[$iProcess]) = 0 Then ContinueLoop
		; strip path from process name
		$aProcessDirs = StringSplit($aProcesses[$iProcess],":\")
		If $aProcessDirs[0] = 0 Then ContinueLoop
		$cProcessName = $aProcessDirs[$aProcessDirs[0]]
		; check an identical given process name only once
		$bAlreadyChecked = False
		If $iProcess > 0 Then
			For $nCheckProcess = 0 To $iProcess-1
				If $aProcesses[$nCheckProcess] = $aProcessDirs[$aProcessDirs[0]] Then
					$bAlreadyChecked = True
					ExitLoop
				EndIf
			Next
		EndIf
		If $bAlreadyChecked Then ContinueLoop	; process has been checked already, don't count
		; count processes
		$aProcess = ProcessList($aProcessDirs[$aProcessDirs[0]])
		If @error Then ContinueLoop
		If $aProcess[0][0] > 0 Then $iCountInstances += $aProcess[0][0]
	Next
	Return $iCountInstances
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProcessChildProcesses
; Description....: Gets child processes of given process (alternative for _WinAPI_EnumChildProcess(), using AutoIt ProcessList() and _WinAPI_GetParentProcess())
; Syntax.........: _ProcessChildProcesses($nProcessId)
; Parameters.....: $nProcessId				- Process id
; Return values..: Array of child processes, empty if no processes or at error (@error = 1)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProcessChildProcesses($nProcessId)
	Local $aProcesses = ProcessList(),$aChildProcesses = [[0,""]],$nChildProcesses = 0

	If @error Then Return SetError(1,0,$aChildProcesses)
	For $nProcess = 1 To $aProcesses[0][0]
		If _WinAPI_GetParentProcess($aProcesses[$nProcess][1]) = $nProcessId Then
			$nChildProcesses += 1
			ReDim $aChildProcesses[$nChildProcesses+1][2]
			$aChildProcesses[$nChildProcesses][0] = $aProcesses[$nProcess][0]
			$aChildProcesses[$nChildProcesses][1] = $aProcesses[$nProcess][1]
		EndIf
	Next
	$aChildProcesses[0][0] = $nChildProcesses
	Return $aChildProcesses
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProcessFirstChildProcess
; Description....: Gets first child process of given process
; Syntax.........: _ProcessFirstChildProcess($nProcessId)
; Parameters.....: $nProcessId				- Process id
; Return values..: Array of child process, empty if no process or at error (@error = 1)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProcessFirstChildProcess($nProcessId)
	Local $aChildProcesses = _ProcessChildProcesses($nProcessId),$aChildProcess = ["",0]

	If @error Then Return SetError(1,0,$aChildProcess)
	$aChildProcess[0] = $aChildProcesses[1][0]
	$aChildProcess[1] = $aChildProcesses[1][1]
	Return $aChildProcess
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _RegistryRead
; Description....: Reads a key value of registry taking in account a 64 bit Windows
; Syntax.........: _RegistryRead($sKey, $sValueName)
; Parameters.....: $sKey					- Registry key
;                  $sValueName				- Value name to read
; Remarks........: For reading some key values from the registry administrator rights could be needed
; Return values..: Read key value
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _RegistryRead($sKey,$sValueName)
	If @OSArch <> "X86" Then $sKey = StringLeft($sKey,StringInStr($sKey,"\")-1) & "64\" & StringTrimLeft($sKey,StringInStr($sKey,"\"))
	Local $vValue = RegRead($sKey,$sValueName)
	If @error = 0 Then Return $vValue
	Return SetError(@error,@extended,$vValue)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _RegistryWrite
; Description....: Writes a key value to the registry taking in account a 64 bit Windows
; Syntax.........: _RegistryWrite($sKey, $sValueName [, $sType = "REG_DWORD" [, $sValue = ""]])
; Parameters.....: $sKey					- Registry key
;                  $sValueName				- Value name to write
;                  $sValue					- Value to write
; Remarks........: For writing some key values to the registry administrator rights could be needed
; Return values..: True if successful
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _RegistryWrite($sKey,$sValueName,$sType = "REG_DWORD",$sValue = "")
	If @OSArch <> "X86" Then $sKey = StringLeft($sKey,StringInStr($sKey,"\")-1) & "64\" & StringTrimLeft($sKey,StringInStr($sKey,"\"))
	Local $bSuccess = RegWrite($sKey,$sValueName,$sType,$sValue)
	If @error = 0 Then Return $bSuccess
	Return SetError(@error,@extended,$bSuccess)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _RegistryDelete
; Description....: Deletes a key value or key from registry taking in account a 64 bit Windows
; Syntax.........: _RegistryDelete($sKey, $sValueName)
; Parameters.....: $sKey					- Registry key
;                  $sValueName				- Value name to delete
; Remarks........: For deleting some keys from the registry administrator rights could be needed
; Return values..: True if successful
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _RegistryDelete($sKey,$sValueName = "")
	If @OSArch <> "X86" Then $sKey = StringLeft($sKey,StringInStr($sKey,"\")-1) & "64\" & StringTrimLeft($sKey,StringInStr($sKey,"\"))
	Local $bSuccess
	If $sValueName = "" Then
		$bSuccess = RegDelete($sKey)
	Else
		$bSuccess = RegDelete($sKey,$sValueName)
	EndIf
	If @error = 0 Then Return $bSuccess
	Return SetError(@error,@extended,$bSuccess)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowsVersionWorld
; Description....: Reads Windows version from registry as presented to the world
; Syntax.........: _WindowsVersionWorld([$bNumber = True])
; Parameters.....: bNumber					- True = number, False = string
; Remarks........: If returning a number, only for use on Windows 7 and higher
; Return values..: Windows version number or string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowsVersionWorld($bNumber = True)
	Local $nBuildNo = _WindowsBuildNumber()
	If $bNumber Then	; Windows 7 and higher
		If $nBuildNo >= 22000 Then Return 11
		If $nBuildNo >= 10240 Then Return 10
		If $nBuildNo >= 9600 Then Return 8.1
		If $nBuildNo >= 9200 Then Return 8
		If $nBuildNo >= 7600 Then Return 7
		Return 0
	Else
		If $nBuildNo >= 22000 Then Return "11"
		If $nBuildNo >= 10240 Then Return "10"
		If $nBuildNo >= 9600 Then Return "8.1"
		If $nBuildNo >= 9200 Then Return "8"
		If $nBuildNo >= 7600 Then Return "7"
		If $nBuildNo >= 6000 Then Return "7"
		If $nBuildNo >= 7601 Then Return "7"
		If $nBuildNo = 3000 Then Return "ME"
		If $nBuildNo >= 2600 Then Return "XP"
		If $nBuildNo = 2200 Then Return "98SE"
		If $nBuildNo = 2195 Then Return "2000"
		If $nBuildNo = 1998 Then Return "98"
		If $nBuildNo = 1381 Then Return "NT4.0"
		If $nBuildNo = 1057 Then Return "NT3.51"
		If $nBuildNo = 950 Then Return "95"
		If $nBuildNo = 807 Then Return "NT3.50"
		If $nBuildNo = 528 Then Return "NT3.1"
		If $nBuildNo = 300 Then Return "3.11"
		If $nBuildNo = 153 Then Return "3.2"
		If $nBuildNo >= 102 Then Return "3.1"
		Return ""
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowsVersion
; Description....: Reads Windows version from registry
; Syntax.........: _WindowsVersion()
; Parameters.....: None
; Remarks........: Windows 11 is registered as being Windows 10!
; Return values..: Windows version number
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowsVersion()
	Local $nVersion = Number(_RegistryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\","CurrentVersion"))
	If $nVersion = 6.3 Then	; Windows 8.1 or higher -> maybe in CurrentMajorVersionNumber
		Local $nVersion2 = _RegistryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\","CurrentMajorVersionNumber")
		If @error = 0 Then Return Number($nVersion2)
	EndIf
	Return $nVersion
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _VersionHigher
; Description....: Checks which of the 2 given version numbers is higher
; Syntax.........: _VersionHigher($sVersion1, $sVersion2 [,$bSign = False])
; Parameters.....: $sVersion1				- version string 1, example "1.9.7.3"
;                  $sVersion2				- version string 2
;                  $bSign					- False returns a number, True returns a sign character
; Return values..: Which version is higher
;                  $bSign false returns 0 = equal, 1 = version 1 is higher, 2 = version 2 is higher
;                  $bSign true returns '=' = equal, '>' = version 1 is higher, '<' = version 2 is higher
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _VersionHigher($sVersion1,$sVersion2,$bSign = False)
	Local $aVersion1,$aVersion2,$nVersion1,$nVersion2

	$aVersion1 = _StringExplode(StringStripWS($sVersion1,$STR_STRIPALL),".")
	$aVersion2 = _StringExplode(StringStripWS($sVersion2,$STR_STRIPALL),".")
	$nVersion1 = UBound($aVersion1)-1
	$nVersion2 = UBound($aVersion2)-1

	If $nVersion1 = $nVersion2 Then			; lengths are equal
		For $nNumber = 0 To $nVersion1
			If Number($aVersion1[$nNumber]) > Number($aVersion2[$nNumber]) Then Return $bSign ? ">" : 1
			If Number($aVersion1[$nNumber]) < Number($aVersion2[$nNumber]) Then Return $bSign ? "<" : 2
		Next
	ElseIf $nVersion1 > $nVersion2 Then		; version length 1 is larger
		For $nNumber = 0 To $nVersion1
			If $nNumber > $nVersion2 Then
				If Number($aVersion1[$nNumber]) > 0 Then Return $bSign ? ">" : 1
				ContinueLoop
			EndIf
			If Number($aVersion1[$nNumber]) > Number($aVersion2[$nNumber]) Then Return $bSign ? ">" : 1
			If Number($aVersion1[$nNumber]) < Number($aVersion2[$nNumber]) Then Return $bSign ? "<" : 2
		Next
	Else									; version length 2 is larger
		For $nNumber = 0 To $nVersion2
			If $nNumber > $nVersion1 Then
				If Number($aVersion2[$nNumber]) > 0 Then Return $bSign ? "<" : 2
				ContinueLoop
			EndIf
			If Number($aVersion1[$nNumber]) > Number($aVersion2[$nNumber]) Then Return $bSign ? ">" : 1
			If Number($aVersion1[$nNumber]) < Number($aVersion2[$nNumber]) Then Return $bSign ? "<" : 2
		Next
	EndIf
	Return $bSign ? "=" : 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowsBuildNumber
; Description....: Reads Windows build number from registry
; Syntax.........: _WindowsBuildNumber()
; Parameters.....: None
; Return values..: Windows build number
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowsBuildNumber()
	Return Number(_RegistryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\","CurrentBuildNumber"))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowsUBRNumber
; Description....: Reads Windows update build release number from registry
; Syntax.........: _WindowsUBRNumber()
; Parameters.....: None
; Return values..: Windows update build release number
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowsUBRNumber()
	Return Number(_RegistryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\","UBR"))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _WindowsScrollInactiveWindow
; Description....: Reads Windows setting to scroll an inactive window
; Syntax.........: _WindowsScrollInactiveWindow()
; Parameters.....: None
; Return values..: True if scrolling an inactive window is allowed
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _WindowsScrollInactiveWindow()
	Return BitAND(Number(_RegistryRead("HKEY_CURRENT_USER\Control Panel\Desktop","MouseWheelRouting")),2) = 2
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LocaleDecimal
; Description....: Returns decimal sign on local computer
; Syntax.........: _LocaleDecimal()
; Parameters.....: None
; Return values..: Decimal sign
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LocaleDecimal()
	Local $LocaleID = _WinAPI_GetUserDefaultLCID()
	Return _WinAPI_GetLocaleInfo($LocaleID,$LOCALE_SDECIMAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LocaleThousand
; Description....: Returns thousands sign on local computer
; Syntax.........: _LocaleThousand($i)
; Parameters.....: None
; Return values..: Thousands sign
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LocaleThousand()
	Local $LocaleID = _WinAPI_GetUserDefaultLCID()
	Return _WinAPI_GetLocaleInfo($LocaleID,$LOCALE_STHOUSAND)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _SoundGetWaveVolume
; Description....: Returns app volume of script, Windows Vista, 7, 8, 10 only
; Syntax.........: _SoundGetWaveVolume([$iValueOnError = -1])
; Parameters.....: $iValueOnError			- Value to return when an error occurs
; Return values..: App volume of script or $iValueOnError at an error
; Error values...: @error = 1				- Unable to create Struct
;                  @error = 2				- Dll file not found
;                  @error = 3				- Wrong call so not on Windows Vista, 7, 8 or 10
;                  @error = 4				- Internal error, array not returned
;                  @error = 5				- Volume wasn't received
;                  @error = 6				- Volume couldn't read
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _SoundGetWaveVolume($iValueOnError = -1)
	Local $LPDWORD,$aMMRESULT,$iVolume

	$LPDWORD = DllStructCreate("dword")
	If @error <> 0 Then
		SetError(1)												; 1 = unable to create Struct
		Return $iValueOnError
	EndIf
	; get app volume of this script
	$aMMRESULT = DllCall("winmm.dll","uint","waveOutGetVolume","ptr",0,"long_ptr",DllStructGetPtr($LPDWORD))
	Switch @error
		Case 1
			SetError(2)											; 2 = dll file not found
			Return $iValueOnError
		Case 2,3,4,5
			SetError(3)											; 3 = wrong call so not on Windows Vista, 7, 8 or 10
			Return $iValueOnError
	EndSwitch
	If not IsArray($aMMRESULT) Then
		SetError(4)												; 4 = internal error, array not returned
		Return $iValueOnError
	EndIf
	If $aMMRESULT[0] <> 0 Then
		SetError(5)												; 5 = volume wasn't received
		Return $iValueOnError
	EndIf
	$iVolume = DllStructGetData($LPDWORD,1)
	If @error <> 0 Then
		SetError(6)												; 6 = volume couldn't read
		Return $iValueOnError
	EndIf
	Return Round(100*$iVolume/4294967295)						; return in range 0 to 100 as SoundSetWaveVolume()
EndFunc