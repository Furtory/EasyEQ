# Peace Function Explanations
provided by NoHarshSounds

This document provides a detailed description for each of the 505 functions found in the `Peace16810.au3` script.

---

### Function 1 to 50

1. `Func EditorRunning($Editor,$PID = 0)`
Checks if a process with the given editor name is running (excluding the given PID), and returns its process ID if found, otherwise returns -1.

2. `Func AverageGUIsBuildTime()`
Calculates and prints the average build time of GUIs, mainly for testing and performance measurement.

3. `Func BackupConfigurationFiles()`
Backs up configuration and configuration set files to a backup folder in the user's Documents directory, if backup is enabled.

4. `Func BackupFiles($Files)`
Copies files matching the given pattern to the backup folder, only if they are new or have been modified since the last backup.

5. `Func SetWorkingDir()`
Sets the working directory to the script's directory.

6. `Func WindowMessagesSystem($Reset = False)`
Registers or unregisters Windows message handlers for inter-process communication and GUI control, depending on the $Reset flag.

7. `Func SetConfigurationsPath($Path = "")`
Sets the path for configuration files, updates related global variables, and writes the path to the INI file if provided.

8. `Func CheckWindowsUpdated()`
Checks if the Windows build or update number has changed since last run, and if so, prompts the user and updates stored version info.

9. `Func UpdateAvailable()`
Checks if a newer version of the program is available by downloading and inspecting the online ReadMe file.

10. `Func ReadMeDownloadCancel()`
Cancels the ongoing download of the ReadMe file and cleans up temporary files.

11. `Func ReadMeDownloaded()`
Handles the completion of the ReadMe file download, checks for a new version, and triggers the update process if needed.

12. `Func VersionHigher($sVersion1,$sVersion2,$bSign = False)`
Compares two version strings and returns which one is higher, or a sign if $bSign is True.

13. `Func DownloadNewVersion($Version)`
Initiates the download and installation process for a new version of the program.

14. `Func ChooseMode()`
Displays a dialog for the user to choose the program mode (e.g., easy or expert).

15. `Func ExtractLanguages()`
Extracts language files from resources or archives for use in the program.

16. `Func DeleteLanguageFiles()`
Deletes any language pack files (.plf) that were extracted during the session to clean up temporary files.

17. `Func AddLanguages()`
Scans the script directory for any new .plf language files and adds them to the list of available languages.

18. `Func NewLanguage($NewLanguage)`
Checks if a given language name already exists in the list of language descriptions.

19. `Func SetLanguage()`
Sets the display language by reading the user's preference from the INI file, or by matching the OS language on first run, then loads the corresponding text from the language file.

20. `Func LanguageNumber($LanguageSearch = $Language)`
Returns the index number of a given language from the language descriptions array.

21. `Func LanguageCode($LanguageSearch = $Language)`
Returns the language code (e.g., "eng") for a given language.

22. `Func Language($LanguageSearch = $Language)`
Returns the full name of a given language (e.g., "English").

23. `Func Text($LineNo,$Insert1 = "",$Insert2 = "",$Insert3 = "",$Insert4 = "")`
Retrieves a line of text from the loaded language array and replaces up to four placeholders ($1, $2, etc.) with provided strings.

24. `Func EqualizerAPOInstallPath()`
Retrieves the installation path of Equalizer APO from the Windows Registry.

25. `Func EqualizerAPOConfigPath()`
Retrieves the configuration file path of Equalizer APO from the Windows Registry.

26. `Func EqualizerAPOVersion()`
Reads the version number of the Equalizer APO DLL file.

27. `Func ConfigMap()`
Checks if the Equalizer APO 'config.txt' file includes the 'peace.txt' file, which is necessary for Peace to function. If not, it prompts the user to add it.

28. `Func MakeIncludeFile($File)`
Creates or overwrites a file (typically 'config.txt') to include the command to load 'peace.txt' and creates a desktop shortcut.

29. `Func ConfigHelp($hWndGUI,$MsgID,$WParam,$LParam)`
Displays a specific help topic related to configuration when a help message is received.

30. `Func ExtractFiles()`
Extracts various necessary files (DLLs, executables, images, etc.) from the compiled executable to the script's directory.

31. `Func LoadImages()`
Initializes GDI+ and loads all required images for the GUI into memory.

32. `Func GraphBrushes($Dispose = False)`
Creates or disposes of GDI+ brushes used for drawing the frequency response graph.

33. `Func GraphBrushesDispose()`
A dedicated function to dispose of all GDI+ brushes used for the graph.

34. `Func ImageEffects()`
Applies visual effects, such as lightening, to the loaded images based on the current skin settings.

35. `Func LoadImage($Image)`
Loads an image, giving priority to a theme-specific version if it exists, otherwise loading the default internal image.

36. `Func LoadImageDetect($Image, ByRef $ThemeImage)`
Detects if a theme-specific version of an image exists and loads it, updating a reference variable.

37. `Func LoadOnOffImage($Load = True,$Dispose = False)`
Loads or disposes of the specific 'on' and 'off' switch images selected by the user in the settings.

38. `Func ClearImages()`
Releases all loaded GDI+ images from memory and shuts down the GDI+ service to free resources.

39. `Func DeleteExtractedFiles()`
Deletes files that were extracted on startup to clean up the application's directory upon exit.

40. `Func ReadSettings()`
Reads all general program settings from the 'peace.ini' file into global variables.

41. `Func WriteSettings()`
Writes the current program settings from global variables into the 'peace.ini' file.

42. `Func WriteBassOnMain()`
Specifically writes the 'Show Bass on Main Interface' setting to the INI file.

43. `Func WriteTrebleOnMain()`
Specifically writes the 'Show Treble on Main Interface' setting to the INI file.

44. `Func WriteBalanceOnMain()`
Specifically writes the 'Show Balance on Main Interface' setting to the INI file.

45. `Func WriteCurrentMidiFile()`
Writes the name of the currently used MIDI settings file to the INI file.

46. `Func WriteExportSettings()`
Saves the last used export format and file path to the INI file.

47. `Func WriteMidiGeneralSettings()`
Saves general MIDI settings, like device and OSD preferences, to the INI file.

48. `Func WriteTopIndex()`
Saves the last viewed top index of the configurations list to the INI file.

49. `Func WriteLargeList()`
Saves the state (large or small) of the configurations list to the INI file.

50. `Func WriteLanguageToTranslate($LanguageTranslate)`
Saves the name of the language currently being translated to the INI file.

### Function 51 to 100

51. `Func WriteBackupRestore()`
Saves the paths for the last backup and restore locations to the INI file.

52. `Func ReadWindowsVersion()`
Reads the stored Windows version, build number, and UBR (Update Build Revision) from the INI file.

53. `Func WriteWindowsVersion()`
Writes the current Windows version, build number, and UBR to the INI file.

54. `Func IniOverrideRead($File,$OverrideFile,$Section,$Key,$DefaultValue)`
Reads a setting from a theme's override INI file; if the setting doesn't exist there, it reads from the main settings INI file.

55. `Func ReadGraphSettings($OverrideFile = $GraphThemeIniFile)`
Reads all settings related to the appearance and behavior of the frequency response graph from the theme or main INI file.

56. `Func WriteGraphSettings($ThemeFile = $GraphThemeIniFile)`
Writes the current graph settings to the specified theme or main INI file.

57. `Func ReadGraphShow()`
Reads the setting that determines if the graph window should be shown by default.

58. `Func WriteGraphShow()`
Writes the setting that determines if the graph window should be shown by default.

59. `Func ReadTestSettings($OverrideFile = $GraphThemeIniFile)`
Reads settings for the headphones and hearing test feature from the theme or main INI file.

60. `Func WriteTestSettings($ThemeFile = $GraphThemeIniFile)`
Writes settings for the headphones and hearing test feature to the specified theme or main INI file.

61. `Func ReadShowDeviceInstalled()`
Reads the setting that controls whether a notification is shown when a new audio device is installed.

62. `Func WriteShowDeviceInstalled()`
Writes the setting that controls whether a notification is shown when a new audio device is installed.

63. `Func ReadProgramMode()`
Reads the user's selected program mode (e.g., Simple or Expert) from the INI file.

64. `Func WriteProgramMode()`
Writes the user's selected program mode to the INI file.

65. `Func ReadCheckNewerVersion()`
Reads the setting that determines if the application should automatically check for new versions.

66. `Func WriteCheckNewerVersion()`
Writes the setting that determines if the application should automatically check for new versions.

67. `Func ReadAntiClipping()`
Reads the anti-clipping feature setting from the INI file.

68. `Func WriteAntiClipping()`
Writes the anti-clipping feature setting to the INI file.

69. `Func ReadGraphicPeak()`
Reads the setting that determines if the graphical peak meter is enabled.

70. `Func WriteGraphicPeak()`
Writes the setting that determines if the graphical peak meter is enabled.

71. `Func WritePeakValueOnTaskBar()`
Writes the setting for showing the peak value on the taskbar to the INI file.

72. `Func ReadMinimized()`
Reads the setting that determines if the application should start minimized to the system tray.

73. `Func WriteMinimized()`
Writes the setting that determines if the application should start minimized to the system tray.

74. `Func ReadHideInterfaces()`
Reads the setting that determines if the main window should be hidden after selecting a configuration from the tray menu.

75. `Func WriteHideInterfaces()`
Writes the setting that determines if the main window should be hidden after selecting a configuration from the tray menu.

76. `Func ReadAutomation()`
Reads all automation settings, such as which configurations to apply when specific programs are running or devices are connected.

77. `Func WriteAutomation()`
Writes all automation settings to the INI file.

78. `Func ReadWindowPosition($Key)`
Reads the saved screen position (X, Y, width, height) for a specific GUI window from the INI file.

79. `Func ReadWindowPositions()`
Reads the saved screen positions for all main GUI windows.

80. `Func WriteWindowPositions($Reset = False)`
Writes the current positions of all main GUI windows to the INI file, or resets them to default if $Reset is true.

81. `Func ReadWindowSettingsPositions()`
Reads the saved screen positions for all settings-related windows.

82. `Func WriteWindowSettingsPositions($Reset = False)`
Writes the current positions of all settings-related windows to the INI file, or resets them.

83. `Func ReadSkin($OverrideFile = $ThemeIniFile)`
Reads all skin and theme settings, such as colors, fonts, and image paths, from the theme or main INI file.

84. `Func WriteSkin($ThemeFile = "")`
Writes the current skin settings to the specified theme or main INI file.

85. `Func ReadThemeSettingsFolder()`
Reads the folder path where theme settings are stored from the INI file.

86. `Func WriteThemeSettingsFolder()`
Writes the folder path where theme settings are stored to the INI file.

87. `Func HotKeyCombiName()`
Creates a user-friendly string representing the currently pressed hotkey combination (e.g., "Ctrl+Alt+P").

88. `Func ReadExampleCommands()`
Reads a list of example Equalizer APO commands from a file into an array.

89. `Func EncryptedPassword($Encrypt)`
Encrypts a given password string.

90. `Func DecryptPassword($Decrypt)`
Decrypts a given encrypted password string.

91. `Func StartupPassword()`
Prompts the user for a password at startup if one is set in the settings.

92. `Func ReadCommandLine()`
Parses the command line arguments to see if a specific configuration should be activated on startup.

93. `Func FirstInstance()`
Checks if this is the first instance of the application running, preventing multiple instances.

94. `Func SetTrayIcon()`
Sets the system tray icon, its tooltip, and state based on the current equalizer status (on, off, muted, etc.).

95. `Func InitTray()`
Initializes the system tray menu, creating all menu items for configurations, settings, and other actions.

96. `Func HideInterfacesAfterSelection()`
Hides the main GUI windows if the setting to do so after a tray menu selection is enabled.

97. `Func SetTrayMouse()`
Configures the system tray icon's mouse-click behavior based on user settings.

98. `Func ShowEffects()`
Shows or hides the effects panel.

99. `Func GoToSettings()`
Opens the settings window.

100. `Func ResetWindowPositions()`
Resets the positions of all GUI windows to their default locations.

### Function 101 to 150

101. `Func TrayGUI()`
Creates a hidden GUI window required for the tray icon and its message handling to function correctly.

102. `Func ToggleGUI()`
Shows or hides the main GUI window.

103. `Func GetIcon($Default = False)`
Returns the appropriate icon file name based on the current equalizer state and user's color preference.

104. `Func SetGUIIcon()`
Sets the icon for the main GUI window.

105. `Func CreateTrayConfigurations($Selected)`
Creates and populates the configurations list in the system tray menu.

106. `Func ShowConfigurationInfo()`
Displays information about the currently active configuration.

107. `Func RestartProgram()`
Restarts the application.

108. `Func ExitProgram()`
Closes the application, performing necessary cleanup tasks.

109. `Func ActivateConfigurationByTray()`
Activates an equalizer configuration selected from the system tray menu.

110. `Func ActivateConfiguration($SelectedConfig,$ShowTip = True,$HideToTray = True)`
The core function for applying an equalizer configuration. It reads the configuration file, updates all GUI controls, generates the necessary command file for Equalizer APO, and optionally shows a notification.

111. `Func ToggleHotkeyConfiguration()`
Switches between a primary and secondary hotkey-assigned configuration.

112. `Func HotKeyConfiguration($HotKeyConfiguration,$Switch = 2)`
Applies a configuration that has been assigned to a specific hotkey.

113. `Func ShowTrayVolume()`
Displays a small, detached window with a pre-amplification slider, typically accessed from the tray.

114. `Func ShowPreAmp()`
Shows or hides the main pre-amplification control GUI.

115. `Func CreateCommandsFile($ForceCreate = False,$Read = True)`
Generates the 'peace.txt' file, which contains all the Equalizer APO commands for the currently active configuration.

116. `Func ReadSettingsVolume()`
Reads the settings for the detached pre-amplification (volume) slider window, such as its position and appearance.

117. `Func ResetSettingsVolume()`
Resets the settings for the detached pre-amplification window to their default values.

118. `Func WriteSettingsVolume($Handle)`
Writes the current settings (like position) of the detached pre-amplification window to the INI file.

119. `Func CtrlLabel($TextNo,$X,$Y,$Width = -1,$Height = -1,$TipTextNo = "",$Style = -1)`
A wrapper function to create a label control on a GUI, using text from the language file.

120. `Func CtrlLabelEnable($LabelID,$Enable = True)`
Enables or disables a label control.

121. `Func CtrlInput($Input,$X,$Y,$Width = -1,$Height = -1,$TipTextNo = "",$InputMask = "")`
A wrapper function to create an input (text box) control on a GUI.

122. `Func FrequencyLines($FrequenciesSet)`
Populates an array with the specific frequencies that will be displayed as vertical lines on the graph.

123. `Func CalculateMeasurementGains($GraphWidth,$GraphHeight,$GraphX,$GraphY)`
Calculates the gain values at specific frequencies for a measured audio response.

124. `Func CalculateMeasureGain($Frequency,$StepsCount)`
Calculates the gain for a single frequency based on measurement data.

125. `Func CalculateGraphLines($GraphWidth,$GraphHeight,$GraphX,$GraphY)`
Calculates the screen coordinates for drawing the grid lines (frequencies and gains) on the analysis graph.

126. `Func CalculateGraphGains($Speaker,$GraphWidth,$DrawHandle = -1)`
Calculates the resulting gain at each frequency for a specific speaker channel by combining the effects of all its filters.

127. `Func DrawGraph($Speaker,$GUIHandle,$GraphHandle,$GraphWidth,$GraphHeight,$GraphX,$GraphY,$DrawLabels = True,$DrawHandle = -1)`
The main function to draw the entire frequency response graph for a selected speaker, including grid, curves, and labels.

128. `Func DrawSlider($Graphics,$Thumb,$Background, ByRef $Sliders,$Slider,$X,$Y,$Width,$Height,$ThumbWidth,$ThumbHeight,$ThumbOffset,$Clear = True,$Hover = False)`
Draws a custom GDI+ slider control, including its background track and thumb.

129. `Func DrawPreAmp($Graphics,$Thumb,$Background,$X,$Y,$Width,$Height,$ThumbWidth,$ThumbHeight,$ThumbOffset,$Clear = True,$Hover = False)`
Draws the custom GDI+ pre-amplification slider.

130. `Func SetTrayTitle($Configuration = "",$PreAmp = "")`
Sets the tooltip text for the system tray icon, displaying the active configuration and pre-amp level.

131. `Func SetEqualizerTitle()`
Sets the title bar text of the main equalizer window to include the application name and active configuration.

132. `Func SetMoveSlider($Slider)`
Sets a flag to indicate which slider is currently being moved by the user.

133. `Func FillRoutingTable(ByRef $RoutingTable, ByRef $MuteTable)`
Populates arrays with the current channel routing and mute settings based on the active configuration.

134. `Func SaveRoutingTable(ByRef $RoutingTable, ByRef $MuteTable,$Check = False)`
Saves the channel routing and mute settings from the arrays back into the main effects configuration.

135. `Func ControlRightPosition($GUI,$ControlID)`
Calculates and returns the screen coordinate of the right edge of a given GUI control.

136. `Func ControlBottomPosition($GUI,$ControlID)`
Calculates and returns the screen coordinate of the bottom edge of a given GUI control.

137. `Func KnobImage($InternalImage,$ThemeImage = "")`
Loads a knob image, preferring a theme-specific version if available.

138. `Func AddControlToTab(ByRef $TabControls,$ControlID,$TabNo,$Reversed = False,$Show = True,$ControllingID = -1,$AlwaysHide = False)`
Assigns a GUI control to a specific tab, managing its visibility when tabs are switched.

139. `Func SetTabControls(ByRef $TabControls,$TabNo,$PreviousTabNo = -1)`
Shows or hides all controls associated with a tab number when the user switches tabs.

140. `Func ShowTabControl(ByRef $TabControls,$ControlID,$Show = True,$Do = True)`
Shows or hides a specific control that is part of a tabbed interface.

141. `Func SetSlidersKeys($Set = True)`
Enables or disables accelerator keys (keyboard shortcuts) for the sliders.

142. `Func SlidersKeysSet0()`
Resets the keyboard state for slider shortcuts.

143. `Func SlidersKeys()`
Handles keyboard input for controlling sliders (e.g., arrow keys to move).

144. `Func CreateNoiseBuffer($DirectSound,$NoiseSamples,$NoiseVolume)`
Creates a DirectSound buffer for playing background noise in the hearing test feature.

145. `Func CreateSineBuffer($DirectSound,$SampleRate,$SineFrequency,$SineVolume)`
Creates a DirectSound buffer for playing a sine wave tone at a specific frequency and volume for testing.

146. `Func CreateDirectSound()`
Initializes the DirectSound object required for audio playback in the testing features.

147. `Func CalculateResultGainLines($GraphWidth,$GraphHeight,$GraphX,$GraphY,$GainType = 0)`
Calculates the screen positions for drawing the lines of a hearing test result on the graph.

148. `Func DrawResult($GUIHandle,$GraphHandle,$GraphWidth,$GraphHeight,$GraphX,$GraphY,$GraphMarginY, ByRef $Contour, ByRef $SineFrequencies, ByRef $CompareResult,$SelectedEar,$DrawDifference = False,$DrawLabels = False)`
Draws the results of a hearing test onto the graph, including the measured contour and comparison data.

149. `Func DrawGraphResult($GraphHandle,$GraphWidth,$GraphHeight,$GraphX,$GraphY,$Frequencies,$GraphColor)`
Draws a single frequency response curve on the graph, used for test results.

150. `Func ResultLabel($ResultLabel,$SelectedEar = 0,$ResultFile = "",$TestChanged = False)`
Updates a label to show the status of the hearing test result (e.g., which ear, file name, unsaved changes).

### Function 151 to 200

151. `Func SetContour(ByRef $Contour,$SelectedContour)`
Applies a selected hearing loss contour to the test data.

152. `Func UseChannels($SelectedEar,$UseBothChannels,$UseLeftRightChannels)`
Determines which audio channels (left, right, or both) should be used for the hearing test based on user selection.

153. `Func VisibilityUseChannels($EnableBoth,$EnableLeftRight,$Reset,$UseButtonCreate,$UseBothChannels,$UseBothChannelsLabel,$UseLeftRightChannels,$UseLeftRightChannelsLabel)`
Manages the visibility and state of the channel selection controls in the test interface.

154. `Func TestInterface()`
Creates and manages the entire GUI for the headphones and hearing test feature.

155. `Func TestMousewheel($hWnd,$iMsg,$wParam,$lParam)`
Handles mouse wheel events specifically for the sliders within the test interface (e.g., adjusting noise or sine levels).

156. `Func ResultToEqualization(ByRef $Frequencies, ByRef $Contour,$CalculateOnly = False,$ChannelType = 0)`
Converts the results of a hearing test into a new equalizer configuration to compensate for hearing loss.

157. `Func FillEffects()`
Populates the controls on the Effects panel (like switches and knobs) with their current values from the active configuration.

158. `Func CreateEffectsGUIs()`
Builds the GUI windows for the main Effects panel and the Surround Effects panel.

159. `Func SurroundDelayChanged()`
Updates the configuration when a surround sound delay value is changed in the GUI.

160. `Func CreateBalanceBar()`
Creates the GUI control for adjusting the left/right audio balance.

161. `Func CreatePreAmpGUIs()`
Builds the GUI windows related to pre-amplification controls.

162. `Func SetPreAmpsOnGUIs($PreAmp)`
Updates all pre-amplification sliders and input boxes across all relevant GUIs with a new value.

163. `Func CreateFilterTypesGUI($dBSliderWidth)`
Creates the popup window that allows the user to select a filter type (e.g., Peak, Low Pass) for a specific EQ band.

164. `Func CreateCommandsGUI()`
Creates the GUI window where the user can enter custom Equalizer APO commands.

165. `Func CreateGraphSettingsGUI()`
Creates the GUI window for customizing the appearance and behavior of the frequency response graph.

166. `Func CreateSpeakersGUI()`
Creates the GUI window for managing speaker setups and channel assignments.

167. `Func MidiSetTitle()`
Sets the title of the MIDI settings window, indicating the current device or file.

168. `Func CreateMidiPanel()`
Builds the main MIDI control panel GUI.

169. `Func ToggleMidiControls()`
Shows or hides the MIDI-related controls on the main interface.

170. `Func CreateMidiSettingsGUI()`
Creates the GUI window for configuring MIDI devices and mapping controls.

171. `Func FillMidiFeature(ByRef $Features,$Feature,$Type,$SubType,$TextNo,$Minimum,$Maximum,$Step,$Display,$Midpoint)`
A helper function to add a specific controllable feature (like preamp or a slider) to the list of available MIDI mappings.

172. `Func FillMidiFeatures(ByRef $Features)`
Populates the MIDI features array with all the parameters that can be controlled via MIDI.

173. `Func MidiCalculateFromPosition($Data,$Controller,$Double = False)`
Translates an incoming MIDI controller value (e.g., 0-127) into the corresponding Peace value for the linked feature.

174. `Func SetMidiLinkFields($Link,$Data = 0)`
Updates the fields in the MIDI settings GUI to reflect the properties of a selected MIDI mapping.

175. `Func MidiControllerString($Controller,$Select = False,$Replace = False)`
Formats a string to display MIDI controller information in a user-friendly way.

176. `Func ShowOSD($Show,$LabelInfo,$LabelValue,$DisableColor = False,$LabelCharacters = 0)`
Shows or hides a custom On-Screen Display (OSD) window to provide visual feedback for changes (e.g., volume change).

177. `Func InitializeGUIOSD($DeleteGUI = True)`
Creates or destroys the GUI window used for the On-Screen Display.

178. `Func ReadMidiDevice($File = "")`
Reads the saved MIDI device from the specified MIDI settings file.

179. `Func ReadMidiSettings($File = "",$Add = False)`
Reads all MIDI control mappings and settings from a specified file into memory.

180. `Func WriteMidiDevice($File,$Device)`
Writes the selected MIDI device to the specified MIDI settings file.

181. `Func WriteMidiSettings($File = "")`
Writes all current MIDI control mappings and settings to a specified file.

182. `Func OpenMidiSettings($File)`
Opens a dialog for the user to select a MIDI settings file to load.

183. `Func StartUpMidi()`
Initializes the MIDI system on application startup, opening the selected device.

184. `Func DetectMidiDevices()`
Scans the system for available MIDI input devices and populates the device list.

185. `Func ShutDownMidi()`
Closes the open MIDI device and releases system resources.

186. `Func CreateGraphGUI()`
Creates the main window that contains the frequency response graph.

187. `Func CreateGraphSelect()`
Creates the small GUI window that allows selecting which speaker channels are visible on the graph.

188. `Func CreateSliderDataGUI()`
Creates the window that displays detailed numerical data for the currently selected slider (frequency, gain, Q).

189. `Func CreateTaskBarObject()`
Creates the necessary taskbar list object for managing taskbar-related features like progress indicators.

190. `Func CreatePeakMeters()`
Creates the GUI elements for the graphical peak value meters.

191. `Func ListBoxColors($hWnd,$Msg,$wParam,$lParam)`
A custom message handler to control the background and text colors of items in a listbox, used for skinning.

192. `Func Equalizer()`
The main function that creates the primary equalizer interface GUI and enters the main event loop to handle user interactions.

193. `Func WindowFromMousePoint()`
Gets the handle of the window currently under the mouse pointer.

194. `Func FindHoverControl()`
Identifies which control on the main GUI is currently under the mouse pointer to show tooltips or hover effects.

195. `Func ShowControlHover()`
Displays a tooltip or hover effect for the control currently under the mouse.

196. `Func ResetControlHover()`
Hides any active tooltips or hover effects.

197. `Func FindHoverSettingsControl()`
Identifies which control on the settings GUI is currently under the mouse pointer.

198. `Func ShowSettingsControlHover()`
Displays a tooltip or hover effect for the control on the settings GUI.

199. `Func ResetSettingsControlHover()`
Hides any active tooltips or hover effects on the settings GUI.

200. `Func RedrawAfterSleep($hWnd,$iMsg,$wParam,$lParam)`
A message handler that forces a redraw of the GUI after the computer wakes from sleep to fix any rendering issues.

### Function 201 to 250

201. `Func ForceExit($hWnd,$Msg,$wParam,$lParam)`
A message handler that ensures the program exits cleanly when a Windows shutdown or logoff event is detected.

202. `Func Donate($Select)`
Opens a specific donation link (like PayPal or a crypto address) in the user's web browser based on their selection.

203. `Func DonateShow($DonateCopy,$Type)`
Displays the GUI window that shows donation options and QR codes.

204. `Func AutoCloseWindow()`
A timer function that automatically closes a temporary window (like the donation window) after a set period.

205. `Func CreateHotkeysArray(ByRef $Hotkeys)`
Initializes and populates an array with all the hotkey assignments for various actions and configurations.

206. `Func SaveHotKey($HotKeysList, ByRef $Hotkeys,$ConfigNo,$ConfigName)`
Saves a new or changed hotkey assignment for a specific configuration to the hotkeys array and the INI file.

207. `Func SetConfigurationHotkey()`
Registers a global hotkey for a specific equalizer configuration.

208. `Func ShortcutConfiguration()`
Creates a desktop shortcut that, when run, applies a specific equalizer configuration.

209. `Func _FileProperName($sFilename)`
A helper function to ensure a filename follows proper capitalization rules.

210. `Func SaveConfiguration(ByRef $ConfigurationId, ByRef $Description, ByRef $WebPage)`
Handles the "Save Configuration" dialog, prompting the user for a name and saving all current EQ settings to a new .peace file.

211. `Func SkinColorTitles()`
Retrieves the names of all available color schemes (skins) to display them in the settings menu.

212. `Func DrawThumbs($GUI,$Graphics,$X,$Y,$OffSetX,$OffSetY,$RowThumbs)`
Draws a row of slider thumb images in the theme selection window to provide a visual preview.

213. `Func DrawSwitches($GUI,$Graphics,$X,$Y,$OffSetX)`
Draws a set of on/off switch images in the theme selection window for preview.

214. `Func FillLanguageText($ControlID,$LanguageNo = -1,$New = False)`
Populates a listbox control with all the text lines from a selected language file for translation purposes.

215. `Func GetLanguageText($CurrentList,$CurrentText,$NewList,$NewText,$NewClicked = False)`
Retrierives the selected text line from a language list and places it in a text input box for editing.

216. `Func CheckLanguageLineExists($Line,$CurrentList)`
Checks if a specific text line number already exists in the language file being edited.

217. `Func Translated($CheckButton,$CurrentList,$NewList)`
Updates the visual state of a checkbox to indicate whether a line has been translated.

218. `Func SaveLanguage($NewCombo,$NewList,$NewTextLine,$NewCount,$NewClicked = False)`
Saves the edited/translated text line back to the corresponding language file.

219. `Func CreateLanguage($FileToCopy = "")`
Creates a new, empty language file or copies an existing one to serve as a template for a new translation.

220. `Func Settings()`
The main function that creates the primary settings window GUI and handles its events.

221. `Func SettingsHandler($hWnd,$iMsg,$wParam,$lParam)`
The message handler for the settings window, processing user interactions with its controls.

222. `Func _GUICtrlListBox_FindInTextEx($hListBox,$sSearchText,$iStart = -1,$bWrapOK = True,$bReverse = False)`
A custom function to search for text within a listbox control, with options for wrapping and direction.

223. `Func LanguageSearch($Search,$List,$Direction = 0)`
Performs a search for text within the language editor's listbox.

224. `Func EnableDisableLanguageTextNew($New,$TextNew)`
Enables or disables the input controls in the language editor.

225. `Func Backup($BSettings,$BConfigurations,$GUISettings)`
Handles the process of backing up settings and/or configurations to a user-selected zip file.

226. `Func Restore($RSettings,$RConfigurations,$GUISettings)`
Handles the process of restoring settings and/or configurations from a user-selected zip file.

227. `Func SetSlider($Set = False)`
Sets the currently selected slider, updating the detailed data window with its specific values (Gain, Frequency, Q).

228. `Func FillHotKeys($ControlID,$aKeys,$DefaultKey)`
Populates a dropdown list with available keys for hotkey assignment.

229. `Func FillKeys($ControlID,$aKeys,$DefaultKey)`
Populates a dropdown list with available modifier keys (Ctrl, Alt, Shift) for hotkey assignment.

230. `Func KeyException($ControlID,$aKeys)`
Handles exceptions for certain key combinations that may be reserved by the system.

231. `Func SetTips($PreAmpOnly = False,$Initialize = False)`
Sets or updates the tooltips for all relevant controls, often to reflect their current values.

232. `Func SetFrequencies($FrequentiesSet)`
Applies a predefined set of frequencies to all the equalizer sliders (e.g., standard 1/3 octave frequencies).

233. `Func AdddBValue($AddValue)`
Adds or subtracts a specific dB value to or from all gain sliders simultaneously.

234. `Func MoveSliders($Left)`
Shifts the entire set of equalizer filter settings to the left or right, effectively moving the curve along the frequency spectrum.

235. `Func SetGraphTitle()`
Updates the title of the graph window to reflect the currently displayed channels.

236. `Func SetSpeaker($Speaker = -1)`
Sets the currently active speaker channel for editing, updating the GUI to show its specific filter settings.

237. `Func SlidersGain($Gain,$Delta = False)`
Sets the gain for all sliders at once, either to an absolute value or by a relative amount.

238. `Func SliderGain($dBSlider,$Gain,$Delta = False,$Minimum = -$dBLimit,$Maximum = $dBLimit)`
Sets the gain for a single, specific slider.

239. `Func SliderFrequency($dBSlider,$Frequency,$Delta = False,$Minimum = 1,$Maximum = $MaxSliderFrequency)`
Sets the center frequency for a single, specific filter slider.

240. `Func SlidersQualities($Quality,$Delta = False)`
Sets the Quality (Q) factor for all filters at once.

241. `Func SliderQuality($dBSlider,$Quality,$Delta = False,$Minimum = 0.01,$Maximum = 0)`
Sets the Quality (Q) factor for a single, specific filter slider.

242. `Func SliderFilter($dBSlider,$FilterNo,$FilterOnOff = 1)`
Changes the filter type (e.g., from Peak to High Pass) for a single, specific slider.

243. `Func SetFiltersOrGraphicEQ($Usage = 0)`
Switches the equalizer mode between a parametric EQ (with selectable filters) and a standard graphic EQ.

244. `Func ExpandSliders($Expand)`
Expands or compresses the overall range of the gain sliders, making adjustments more or less sensitive.

245. `Func FlattenSliders()`
Resets the pre-amp, delay, and all gain sliders to zero, creating a flat equalization.

246. `Func SwapSliders($Direction)`
Swaps the settings of the currently selected slider with its adjacent slider to the left or right.

247. `Func CreateSpeakerList($ControlID,$SelectSpeaker)`
Populates a dropdown list with all available speaker channels.

248. `Func ShowFilter($ControlID,$ShowSpeaker)`
Updates the filter display to show the type of filter being used for the currently selected speaker channel.

249. `Func Equalized($CheckPreAmp = True,$CheckEffects = False)`
Checks if any equalization (gain, preamp, or effects) is currently active across any speaker channel. Returns true if so.

250. `Func SpeakerEqualized($Speaker,$CheckPreAmp = True)`
Checks if a specific speaker channel has any active equalization.

### Function 251 to 300

251. `Func CalculateQuality($k,$n)`
Calculates the required Q factor for Butterworth and Linkwitz-Riley filters based on the filter's order and specific parameters.

252. `Func SetSampleRate()`
Sets the global sample rate variable, reading it from the default rendering device if necessary.

253. `Func CalculateCoefficients($Speaker)`
Calculates the biquad filter coefficients for all active filters on a specific speaker channel. These coefficients are what Equalizer APO uses to process the audio.

254. `Func CalculateCoefficient($FilterNo,$FilterType,$dBGain,$Frequency,$Quality)`
Calculates the specific biquad filter coefficients for a single filter based on its type, gain, frequency, and Q factor.

255. `Func CalculateBassTrebleCoefficients()`
Calculates the biquad filter coefficients for the dedicated bass and treble shelf filters.

256. `Func GainAtFrequencySlider($FilterNo,$FilterType,$Frequency,$Quality = 4)`
Calculates the theoretical gain contribution of a single filter at a specific frequency.

257. `Func GainAtFrequencyBassTreble($AddBass,$Frequency)`
Calculates the theoretical gain contribution of the bass or treble filter at a specific frequency.

258. `Func GainAtFrequency($Phi,$F0,$F1,$F2,$F3,$F4,$F5)`
Calculates the total gain at a given frequency by summing up the complex response of all filter coefficients.

259. `Func LogX($Number,$BaseNumber)`
Calculates the logarithm of a number with a specified base.

260. `Func Log10($Number)`
Calculates the base-10 logarithm of a number.

261. `Func Log2($Number)`
Calculates the base-2 logarithm of a number.

262. `Func OpenConfigurator()`
Launches the Equalizer APO Configurator application (Configurator.exe).

263. `Func FillAutomationDevicesList($ControlID,$DeviceID = "")`
Populates a listbox control with all available audio devices for use in the automation settings.

264. `Func GetVoicemeeterDevices()`
Specifically enumerates and retrieves a list of Voicemeeter audio devices.

265. `Func VoicemeeterDevice($Device,$OutputNo = 0)`
Formats a string for a Voicemeeter device to be used in Equalizer APO commands.

266. `Func FillDevicesList($ControlID, ByRef $DeviceIDs)`
Populates a dropdown list with all detected audio playback devices on the system.

267. `Func _GUICtrlComboBox_GetString($hControl,$IndexNo)`
A helper function to get the string text of an item in a combobox control at a specific index.

268. `Func DefaultDeviceName($DefaultID)`
Retrieves the user-friendly name of the system's default audio device.

269. `Func OpenAudioPanel()`
Opens the Windows audio devices control panel.

270. `Func MMEnumerator()`
Initializes the Windows Multimedia (MM) device enumerator object to get a list of audio devices.

271. `Func MMEnumerate()`
Enumerates all active multimedia audio endpoint devices and stores their properties in a global array.

272. `Func DefaultRenderSampleRate()`
Retrieves the default sample rate of the system's primary audio rendering device.

273. `Func DefaultRenderAudioDevice()`
Retrieves the COM object for the system's default audio rendering (playback) device.

274. `Func DefaultCaptureAudioDevice()`
Retrieves the COM object for the system's default audio capture (recording) device.

275. `Func AudioDeviceID($oDevice)`
Gets the unique device ID string for a given audio device COM object.

276. `Func _WinAPI_PKEYFromString($sPKEY,$pID = Default)`
A helper function to convert a string representation of a property key into a PKEY structure for API calls.

277. `Func _WinAPI_PtrStringLenW($pString)`
A helper function to get the length of a wide-character string from a memory pointer.

278. `Func SwitchOnOff()`
Toggles the master on/off state of the equalizer.

279. `Func EqualizerOnOffByHotKey()`
Toggles the equalizer's on/off state when the corresponding hotkey is pressed.

280. `Func EqualizerOnOff($Sound = False)`
The core function to turn the equalizer on or off, optionally playing a sound for feedback.

281. `Func MuteUnmute()`
Toggles the master mute state of the audio.

282. `Func MuteByKey()`
Toggles the mute state when the corresponding hotkey is pressed or tray icon is clicked.

283. `Func ShowMuteTray()`
Updates the system tray icon to reflect the current mute state.

284. `Func PreAmpUpByKey()`
Increases the pre-amplification level when the corresponding hotkey is pressed.

285. `Func PreAmpDownByKey()`
Decreases the pre-amplification level when the corresponding hotkey is pressed.

286. `Func ChangePreAmp($StepValue,$ShowOSD = 0,$Absolute = False,$MainPreAmp = True,$Minimum = -$dBPreAmpLimit,$Maximum = $dBPreAmpLimit)`
The core function to change the pre-amplification level by a specific step value, with options for OSD feedback.

287. `Func BalanceRightByKey()`
Shifts the audio balance to the right when the corresponding hotkey is pressed.

288. `Func BalanceLeftByKey()`
Shifts the audio balance to the left when the corresponding hotkey is pressed.

289. `Func ChangeBalance($StepValue,$ShowOSD = 0,$Relative = True,$Minimum = -1,$Maximum = 1)`
The core function to change the left/right balance by a specific step value.

290. `Func CopyDataMessage($hWnd,$Msg,$wParam,$lParam)`
Handles WM_COPYDATA messages, allowing Peace to be controlled by external applications.

291. `Func ActionDone($ReturnValue)`
Decrements a process counter to signal that a requested action is complete, used for inter-process communication.

292. `Func MainActions($hWnd,$Msg,$wParam,$lParam)`
A generic message handler for main application-level actions sent via WM_APP messages.

293. `Func InterfaceActions($hWnd,$Msg,$wParam,$lParam)`
A message handler for actions that affect the GUI, like showing or hiding windows.

294. `Func ConfigurationSwitch($hWnd,$Msg,$wParam,$lParam)`
A message handler for switching between equalizer configurations.

295. `Func EqualizerState($hWnd,$Msg,$wParam,$lParam)`
A message handler for querying or changing the equalizer's on/off state.

296. `Func MainRenderDevice($hWnd,$Msg,$wParam,$lParam)`
A message handler for actions related to the main audio rendering device.

297. `Func MainPreAmplifying($hWnd,$Msg,$wParam,$lParam)`
A message handler for changing the main pre-amplification level.

298. `Func EffectsPanel($hWnd,$Msg,$wParam,$lParam)`
A message handler for showing, hiding, or manipulating the effects panel.

299. `Func SetBassGain($Gain,$Delta = False,$Minimum = -20,$Maximum = 20)`
Sets the gain for the dedicated bass filter, either to an absolute value or by a relative amount.

300. `Func SetBassFrequency($Frequency,$Delta = False,$Minimum = 1,$Maximum = 10000)`
Sets the crossover frequency for the dedicated bass filter.

### Function 301 to 350

301. `Func SetTrebleFrequency($Frequency,$Delta = False,$Minimum = 500,$Maximum = 20000)`
Sets the crossover frequency for the dedicated treble filter.

302. `Func DeviceActions($hWnd,$Msg,$wParam,$lParam)`
A message handler for actions related to audio device management.

303. `Func TargetSpeaker($hWnd,$Msg,$wParam,$lParam)`
A message handler for setting the target speaker channel for subsequent equalization commands.

304. `Func ProcessSpeaker($TargetSpeaker,$SwitchOnOff)`
Applies an on/off state to a specific speaker channel.

305. `Func ProcessIsSpeaker($TargetSpeaker)`
Checks if a given target is a valid speaker channel.

306. `Func PreAmplifying($hWnd,$Msg,$wParam,$lParam)`
A message handler for setting the pre-amplification level for a specific speaker channel.

307. `Func EqualizationSliders($hWnd,$Msg,$wParam,$lParam)`
A message handler for setting the gain, frequency, and Q for all sliders of a speaker channel at once.

308. `Func EqualizationSlider($hWnd,$Msg,$wParam,$lParam)`
A message handler for setting the gain, frequency, and Q for a single, specific slider.

309. `Func SetConfigurationById($Id)`
Activates an equalizer configuration by its unique ID (filename without extension).

310. `Func SetConfigurationByKey()`
Activates the equalizer configuration associated with the hotkey that was just pressed.

311. `Func RestartGUI()`
Closes and reopens the main GUI, effectively refreshing the entire interface.

312. `Func FillSounds($ControlID,$SoundFile,$Select)`
Populates a dropdown list with available sound files for UI feedback events.

313. `Func SoundGetWaveVolume($ValueOnError = -1)`
Gets the current application-specific volume level from the Windows audio mixer.

314. `Func KillSound()`
Stops any currently playing feedback sound.

315. `Func PlaySound($Sound,$Volume = 100,$NoPlayIfPlaying = False,$Silence = True)`
Plays a specified .wav file for UI feedback.

316. `Func ConfigurationStripName($Config)`
Removes the hotkey assignment string from a configuration's name.

317. `Func ConfigurationName()`
Gets the clean name of the currently selected configuration from the listbox.

318. `Func ConfigurationHotKey()`
Gets the hotkey string assigned to the currently selected configuration.

319. `Func ConfigurationStripHotKey($ConfigName)`
Extracts just the hotkey part (e.g., "^!P") from a full configuration name string.

320. `Func ImportFilterFile(ByRef $File,$Speaker)`
Imports filter settings from a file (e.g., from REW) and applies them to a specific speaker channel.

321. `Func SpeakerNameFromChannel($Channels)`
Converts a channel code (e.g., "L R") into a user-friendly speaker name (e.g., "Stereo").

322. `Func StringToNumber($NumberString,$REWversion,$Thousand,$Decimal)`
Converts a number formatted as a string, which may have thousand/decimal separators, into a standard number.

323. `Func StringStripString($String,$StripString)`
Removes all occurrences of a specific substring from a given string.

324. `Func ImportREWMeasurement()`
Opens a file dialog for the user to import a measurement file from Room EQ Wizard (REW).

325. `Func GraphOverlay()`
Toggles the display of an overlay graph (like a measurement file) on the main frequency response graph.

326. `Func GUIAutoEQ()`
Creates and manages the GUI window for the AutoEQ feature, which allows importing headphone correction profiles.

327. `Func AutoEQInformation()`
Displays an informational popup about the AutoEQ feature.

328. `Func RichTextAddLine($Info,$Size,$Font,$Text,$Lines = 1)`
A helper function to add a formatted line of text to a rich edit control.

329. `Func WM_NOTIFY_LINK($hWnd,$iMsg,$wParam,$lParam)`
A message handler that detects when a hyperlink inside a rich edit control is clicked.

330. `Func CreateIncludeFile($Device)`
Creates an Equalizer APO include file specifically for an AutoEQ device profile.

331. `Func AutoEQSearchDevice($SearchWay = 0)`
Searches the AutoEQ database for a headphone profile based on user input.

332. `Func WM_COMMAND($hWnd,$iMsg,$wParam,$lParam)`
A generic message handler for command events from GUI controls like buttons and menus.

333. `Func AutoEQInfo($Message = "")`
Displays status messages or errors within the AutoEQ window.

334. `Func DownloadAutoEQVersion()`
Initiates the download of the latest AutoEQ database version file.

335. `Func AutoEQVersionFileDownload()`
Handles the progress and completion of the AutoEQ version file download.

336. `Func CancelDownloadAutoEQVersionFile()`
Cancels the download of the AutoEQ version file.

337. `Func DownloadAutoEQFiles()`
Initiates the download of the main AutoEQ database archive.

338. `Func AutoEQFilesDownload()`
Handles the progress and completion of the main AutoEQ database download.

339. `Func CancelDownloadAutoEQFiles()`
Cancels the download of the main AutoEQ database.

340. `Func AutoEQDecompressFile()`
Decompresses the downloaded AutoEQ database archive.

341. `Func AutoEQLoadFile()`
Loads the decompressed AutoEQ database file into memory for searching.

342. `Func AutoEQFilters($Device)`
Extracts the specific filter settings for a selected device from the AutoEQ database.

343. `Func AutoEQCutFilter($Line)`
Parses a single filter line from an AutoEQ profile.

344. `Func AutoEQHaveQuality($Line)`
Checks if a filter line in an AutoEQ profile contains a Q (quality) value.

345. `Func ApplyEqualization($Device)`
Applies the filter settings from a selected AutoEQ profile to the current equalizer configuration.

346. `Func ReadConfigurationFiles()`
Reads all .peace and .peaceset files from the configurations folder into arrays.

347. `Func FillConfigurationsList(ByRef $Files,$Count,$ListBox,$Selection,$TopIndex,$ShowActive,$SetHotKeys = True)`
Populates a listbox control with the names of all available equalizer configurations.

348. `Func IsAlwaysActive($Configuration)`
Checks if a given configuration is marked as "Always Active".

349. `Func AlwaysActive($Configuration)`
Applies an "Always Active" configuration, which is loaded at startup and persists.

350. `Func FillConfigurationsLists($Selection,$TopIndex = -1)`
A master function to populate all configuration lists across the entire GUI (main window, tray menu, etc.).

### Function 351 to 400

351. `Func SetConfiguration()`
The main function to apply a selected configuration. It reads the file, updates all GUI controls, and generates the Equalizer APO command file.

352. `Func ReadFile($File)`
Reads an entire equalizer configuration (.peace file) into the program's internal arrays and variables.

353. `Func FillControls($Speaker)`
Populates all GUI controls (sliders, inputs, checkboxes) with the values from the currently loaded configuration for a specific speaker.

354. `Func EnableEqualizer($Speaker)`
Enables or disables the equalizer sliders and input boxes for a specific speaker channel based on whether it is active.

355. `Func ControlDisableOnce($ControlID)`
A helper function to disable a GUI control only if it's not already disabled, preventing unnecessary screen flicker.

356. `Func ControlEnableOnce($ControlID)`
A helper function to enable a GUI control only if it's not already enabled, preventing screen flicker.

357. `Func EnableGraphicEQbutton()`
Enables or disables the "Graphic EQ" button based on whether the current configuration uses parametric or graphic EQ filters.

358. `Func ShowAutomationButton()`
Shows or hides the automation button on the main interface based on user settings.

359. `Func FillFilters($ControlID)`
Populates the filter type selection dropdown with all available filter types (Peak, Low Pass, etc.).

360. `Func FillExamples($ControlID)`
Populates the list in the Commands window with example Equalizer APO commands.

361. `Func FillSpeakers($SpeakerTargets = "")`
Updates the speaker channel buttons and controls to reflect the currently active speaker setup.

362. `Func SpeakersEnable($Enable = True)`
Enables or disables all speaker selection and management controls.

363. `Func IsCopySpeakerCommand($SpeakerTargets)`
Checks if the current speaker command is a "copy to another speaker" command.

364. `Func CreateSpeakerTargets()`
Generates the speaker target command string (e.g., "channel: L R") for the Equalizer APO file based on the selected speaker setup.

365. `Func SpeakerTargets($All,$Select,$L,$C,$R,$SUB,$SL,$SR,$RL,$RC,$RR,$Copy,$CopyCommand)`
Constructs the speaker target string based on a series of boolean inputs representing each channel.

366. `Func NewAfterSpeakers($InsertAfterSpeaker,$Before = False)`
Adds a new, empty speaker channel to the configuration, either before or after a selected speaker.

367. `Func RemoveSpeaker($RemoveSpeaker)`
Removes a selected speaker channel from the configuration.

368. `Func CopySpeaker($Speaker)`
Copies all filter settings from a selected speaker channel to the clipboard.

369. `Func PasteSpeaker($Speaker)`
Pastes the copied filter settings to a selected speaker channel.

370. `Func FillEqualizerArray($Speaker)`
Populates the main equalizer array with the filter values of a specific speaker channel.

371. `Func ProcessSliderChanged($Speaker)`
A handler function that is called whenever any slider, pre-amp, or filter value is changed, triggering updates to the graph and configuration state.

372. `Func MouseWheelOnSlider()`
Checks if the mouse wheel is currently being used over a slider control.

373. `Func ProcessPreAmpInterfaces()`
Updates all pre-amp related GUI elements across the application to ensure they are synchronized.

374. `Func CommandsEntered($Commands,$CommandsOn)`
Updates the visual indicator (e.g., a button's appearance) to show whether custom commands have been entered.

375. `Func HaveCommands()`
Checks if the current configuration contains any custom Equalizer APO commands.

376. `Func HaveEffects()`
Checks if the current configuration has any active audio effects (like surround or crossfeed).

377. `Func SetEffectsButtons()`
Updates the appearance of the effects buttons to show whether they are on or off.

378. `Func SurroundSoundShowHide()`
Shows or hides the detailed surround sound settings panel.

379. `Func UpmixShowHide()`
Shows or hides the detailed upmix settings panel.

380. `Func MidsideShowHide()`
Shows or hides the detailed mid/side processing panel.

381. `Func FillProcessList($ControlID,$ForceRefresh = False)`
Populates a list control with all currently running processes for use in the automation settings.

382. `Func SaveFile($File,$Key = "",$ConfigurationId = "",$Description = "",$WebPage = "")`
The core function to write the current equalizer settings from memory into a .peace configuration file.

383. `Func CopyFilterFile()`
Copies the generated 'peace.txt' file to the Equalizer APO config directory to activate the new settings.

384. `Func CreateMuteFile()`
Creates a 'peace.txt' file that contains only a command to mute the audio.

385. `Func CreateEmptyFilterFile($Empty = False)`
Creates a 'peace.txt' file with no filter commands, effectively passing through unfiltered audio.

386. `Func Export()`
Opens the GUI window that allows exporting the current EQ curve to other formats (e.g., for Audacity).

387. `Func AudacityExportTxt($FileName,$Type)`
Exports the current EQ curve to a text file compatible with newer versions of the Audacity Equalizer plugin.

388. `Func AudacityExportXml($FileName)`
Exports the current EQ curve to an XML file compatible with older versions of the Audacity Equalizer plugin.

389. `Func DeviceListString($DeviceNo,$ID = False)`
Formats a string for display in the device list, showing either the device name or its ID.

390. `Func DeviceString()`
Creates the full 'Device:' command string for the 'peace.txt' file based on the automation settings.

391. `Func DeviceCreateString($DeviceNo)`
Creates the 'Device:' string for a single specified device.

392. `Func DeviceState($State)`
Converts a device state code into a user-friendly string (e.g., "Active", "Disabled").

393. `Func DeviceInstalled($GUID,$Device)`
Checks if a specific audio device is currently installed and active on the system.

394. `Func ShowDeviceInstalled($GUID,$Device)`
Shows a notification if a new audio device has been detected.

395. `Func AutomationWriteConfigurations()`
Writes the list of configurations used in automation settings to the INI file.

396. `Func AutomationDeleteConfiguration($ConfigurationDelete)`
Removes a deleted configuration from all automation rules.

397. `Func AutomationRenameConfiguration($Selected,$ConfigurationRename)`
Updates the name of a configuration in all automation rules where it is used.

398. `Func AutomationReadActiveAlways($FillControl)`
Reads the "Always Active" configurations from the INI file and populates the corresponding GUI control.

399. `Func AutomationReadActiveAlwaysConfigurations($Message = False)`
Applies any configurations that are marked as "Always Active".

400. `Func ConfigurationsSetName($SetName)`
Sets the name for a new or existing set of configurations.

### Function 401 to 450

401. `Func ReadConfigurationsSets($SetName = "")`
Reads the definitions of configuration sets from the INI file. A set is a group of configurations that can be activated together.

402. `Func ConfigurationsSetsRenameConfiguration($Selected,$ConfigurationRename)`
Renames a configuration within all defined configuration sets.

403. `Func ConfigurationsSetsDeleteConfiguration($Selected)`
Deletes a configuration from all defined configuration sets.

404. `Func ConfigurationsSetByKey()`
Activates a configuration set that is assigned to a specific hotkey.

405. `Func ActivateConfigurationsSet()`
The main function to activate a selected configuration set, applying all its contained configurations.

406. `Func SetConfigurationConfigurationsSet($ShowTip = False,$HideToTray = False)`
Applies the currently selected configuration set.

407. `Func ReadConfigurationsSet()`
Reads the currently active configuration set from the INI file.

408. `Func ConfigurationsSetSaveButton($Reset = False)`
Handles the logic for the "Save Set" button, enabling or disabling it based on whether changes have been made.

409. `Func SaveConfigurationsSet()`
Saves the current definition of a configuration set to the INI file.

410. `Func DeleteConfigurationsSet()`
Deletes a defined configuration set.

411. `Func AutomationReadActiveHotkeys($SaveCurrent)`
Reads all hotkey assignments from the INI file and registers them with the system.

412. `Func AutomationReadActiveHotkeysConfigurations($Message = 0)`
Applies the configurations associated with any active hotkeys.

413. `Func AutomationReadProcesses()`
Reads the list of process-based automation rules from the INI file.

414. `Func AutomationFillProcess()`
Populates the GUI list with the defined process-based automation rules.

415. `Func AutomationAddProcess($Configuration,$Process)`
Adds a new rule to automatically apply a configuration when a specific process starts.

416. `Func AutomationDeleteProcess()`
Deletes a selected process-based automation rule.

417. `Func AutomationReadDevices()`
Reads the list of device-based automation rules from the INI file.

418. `Func AutomationFillDevices()`
Populates the GUI list with the defined device-based automation rules.

419. `Func AutomationAddDevice($ConfigurationControl,$DeviceControl,$DeviceIDs)`
Adds a new rule to automatically apply a configuration when a specific audio device becomes active.

420. `Func AutomationDeleteDevice()`
Deletes a selected device-based automation rule.

421. `Func AddToFilterFile(ByRef $Configuration)`
Appends the filter commands for a given configuration to the main 'peace.txt' file content.

422. `Func UpmixBass()`
Adds specific commands to the filter file to handle bass redirection in an upmix scenario.

423. `Func CreateFilterFile()`
The master function that assembles all parts (device commands, preamp, effects, filters) and creates the final 'peace.txt' file for Equalizer APO.

424. `Func StringMax($Value,$Decimals = 2,$MaxValue = 1)`
Formats a string to represent a number, ensuring it does not exceed a maximum value and has a specific number of decimal places.

425. `Func ReverseChannels()`
Adds a command to the filter file to swap the left and right audio channels.

426. `Func SurroundEffect(ByRef $SurroundEffect)`
Adds the commands for the selected virtual surround sound effect to the filter file.

427. `Func Effects(ByRef $Effects)`
Adds commands for various audio effects (like crossfeed, upmix) to the filter file.

428. `Func DeleteLog()`
Deletes the program's log file.

429. `Func WriteToLog($Text)`
Writes a line of text to the program's log file for debugging purposes.

430. `Func Help($HelpFile,$Topic = "")`
Opens the application's help file (.chm), optionally navigating to a specific topic.

431. `Func SetF1($HelpFile,$Topic)`
Assigns a specific help topic to be opened when the F1 key is pressed.

432. `Func ProgramIsRunning($Program32,$Program64)`
Checks if a given program is running, checking for both 32-bit and 64-bit versions.

433. `Func Delta128($X,$Y)`
Calculates the shortest distance between two values on a circular scale of 0-127, used for MIDI rotary encoders.

434. `Func Decimals($nValue)`
Counts the number of decimal places in a given number.

435. `Func Clamp($Value,$MinValue,$MaxValue)`
Constrains a value to be within a specified minimum and maximum range.

436. `Func Max($Number1,$Number2)`
Returns the larger of two numbers.

437. `Func Min($Number1,$Number2)`
Returns the smaller of two numbers.

438. `Func StringRemoveCharacters($String,$Characters)`
Removes a specific set of characters from a string.

439. `Func StringRepeat($String,$Count)`
Repeats a given string a specified number of times.

440. `Func StringEmpty($String)`
Checks if a string is null or contains only whitespace.

441. `Func StringTidy($String)`
Trims leading/trailing whitespace and reduces multiple internal spaces to a single space.

442. `Func StringStrip($String,$Chars)`
Removes all occurrences of specified characters from a string.

443. `Func StringBetweenDelimiters($String,$StartDelimiter,$EndDelimiter)`
Extracts the substring located between two specified delimiter strings.

444. `Func StringDelimiter($String,$Delimiter)`
Gets the part of a string that comes before the first occurrence of a specified delimiter.

445. `Func StringFromDelimiter($String,$Delimiter)`
Gets the part of a string that comes after the first occurrence of a specified delimiter.

446. `Func ArrayToString(ByRef $StringArray,$Delimiter,$Count,$Column = -1)`
Converts a 1D or 2D array of strings into a single, delimiter-separated string.

447. `Func FileGetSizeTimed($File,$GetSizeTimeOut = 5000,$GetSizeWait = 100)`
Attempts to get the size of a file, waiting for a short period if the file is locked or still being written.

448. `Func FileName($File)`
Extracts the name of a file from its full path, without the extension.

449. `Func FileNext($File)`
Finds the next file alphabetically in the same directory.

450. `Func FilePrevious($File)`
Finds the previous file alphabetically in the same directory.

### Function 451 to 505

451. `Func FileAddExtension($File,$Extension)`
Adds or replaces the extension of a given filename.

452. `Func FileAddPath($File,$Path)`
Combines a filename and a path into a full file path string.

453. `Func LocaleDecimal()`
Returns the character used as the decimal separator based on the user's regional settings.

454. `Func LocaleThousand()`
Returns the character used as the thousands separator based on the user's regional settings.

455. `Func GUIResize($hGUI,$nWidth,$nHeight,$bDelta = False)`
Resizes a GUI window to a specified width and height.

456. `Func GUIGetBkColor($Handle)`
Gets the background color of a GUI window or control.

457. `Func MousewheelRegister()`
Registers a hook to intercept system-wide mouse wheel messages.

458. `Func MousewheelUnregister()`
Unregisters the mouse wheel message hook.

459. `Func _MousewheelDetect($hWnd,$iMsg,$wParam,$lParam)`
The core hook function that captures mouse wheel events and updates global variables with the direction and key states.

460. `Func MousewheelGUI()`
Checks if the mouse wheel was scrolled over the application's GUI.

461. `Func MousewheelUp()`
Checks if the last detected mouse wheel action was scrolling up.

462. `Func MousewheelDown()`
Checks if the last detected mouse wheel action was scrolling down.

463. `Func MousewheelIdle()`
Checks if there has been no mouse wheel action since the last reset.

464. `Func MousewheelMouseLeft()`
Checks if the left mouse button was held down during the last mouse wheel action.

465. `Func MousewheelMouseRight()`
Checks if the right mouse button was held down during the last mouse wheel action.

466. `Func MousewheelMouseMiddle()`
Checks if the middle mouse button was held down during the last mouse wheel action.

467. `Func MousewheelKeyShift()`
Checks if the Shift key was held down during the last mouse wheel action.

468. `Func MousewheelKeyControl()`
Checks if the Control key was held down during the last mouse wheel action.

469. `Func MousewheelReset()`
Resets all mouse wheel status variables, preparing for the next detection.

470. `Func GUIMouseControl($Handle)`
Returns the control ID of the GUI element currently under the mouse cursor.

471. `Func GUIMouseDown($Handle,$Button)`
Checks if a specific mouse button is currently being held down over a GUI window.

472. `Func GUIMouseGetX($Handle)`
Gets the mouse cursor's X coordinate relative to a GUI window.

473. `Func GUIMouseGetY($Handle)`
Gets the mouse cursor's Y coordinate relative to a GUI window.

474. `Func WindowNotOnMonitor($Handle)`
Checks if a window's position is outside of any connected monitor's display area and moves it back to the primary monitor if it is.

475. `Func WindowMinimized($Handle)`
Checks if a given window is currently minimized.

476. `Func WindowClientWidth($Handle)`
Returns the width of a window's client area (the usable space inside the borders).

477. `Func WindowClientHeight($Handle)`
Returns the height of a window's client area.

478. `Func WindowGetX($Handle)`
Returns the X screen coordinate of a window's top-left corner.

479. `Func WindowGetY($Handle)`
Returns the Y screen coordinate of a window's top-left corner.

480. `Func WindowWidth($Handle)`
Returns the total width of a window, including its borders.

481. `Func WindowHeight($Handle)`
Returns the total height of a window, including its title bar and borders.

482. `Func WindowBordersWidth($Handle)`
Calculates the combined width of the left and right window borders.

483. `Func WindowBordersHeight()`
Calculates the combined height of the top (title bar) and bottom window borders.

484. `Func WindowBorderWidth($Handle)`
Calculates the width of a single vertical window border.

485. `Func WindowBorderHeight($Handle)`
Calculates the height of a single horizontal window border.

486. `Func WorkAreaWidth()`
Returns the width of the primary monitor's work area (the screen minus the taskbar).

487. `Func WorkAreaHeight()`
Returns the height of the primary monitor's work area.

488. `Func WorkAreaWidthAtPoint($X,$Y)`
Returns the width of the work area of the monitor at a given screen coordinate.

489. `Func WorkAreaHeightAtPoint($X,$Y)`
Returns the height of the work area of the monitor at a given screen coordinate.

490. `Func ScrollbarOffsetX()`
Calculates the horizontal offset needed for controls when a vertical scrollbar is present.

491. `Func ScrollbarOffsetY()`
Calculates the vertical offset needed for controls when a horizontal scrollbar is present.

492. `Func DPIRatio()`
Calculates the current monitor's DPI scaling ratio relative to the standard 96 DPI.

493. `Func ColorRGBtoBGR($Color)`
Converts a color value from RGB (Red, Green, Blue) format to BGR (Blue, Green, Red) format.

494. `Func ColorLighten($Color,$ColorFactor)`
Lightens a given color by a specified factor.

495. `Func ColorBrighten($Color,$ColorDelta)`
Brightens or darkens a color by adding a delta value to its RGB components.

496. `Func ColorMix($Color1,$Color2,$Mix = 1)`
Mixes two colors together at a specified ratio.

497. `Func GetScrollInactiveWindowsSetting()`
Checks the Windows setting that controls whether inactive windows can be scrolled with the mouse wheel.

498. `Func WindowsVersion()`
Returns the major, minor, and build numbers of the current Windows version as a single numerical value.

499. `Func WindowsBuildNumber()`
Reads and returns the Windows build number from the registry.

500. `Func WindowsUBRNumber()`
Reads and returns the Windows Update Build Revision (UBR) number from the registry.

501. `Func _GUICtrlGetType($hControl,$bTypeID = True,$bDetail = False)`
A helper function to get the type of a GUI control (e.g., button, input).

502. `Func Message($sMessage,$sTitle,$sHideText, ByRef $bHideMessage,$sOkButtonText = "Ok",$sOkTooltip = "",$sCancelButtonText = "",$sCancelTooltip = "",$iGUIWidth = 400,$iGUIHeight = 120,$iMargin = 15,$iButtonWidth = 100,$iButtonHeight = 25,$iControlHeight = 23)`
Creates a flexible, custom message box with more options than the standard MsgBox function.

503. `Func _RegistryRead($sKey,$sValueName)`
A wrapper function to read a value from the Windows Registry, checking both HKLM64 and HKLM hives.

504. `Func Console($vText1,$vText2 = Default,$vText3 = Default,$vText4 = Default,$vText5 = Default,$vText6 = Default,$vText7 = Default,$vText8 = Default,$vText9 = Default,$vText10 = Default)`
Writes up to 10 lines of text to the console output for debugging purposes.

505. `Func _WinAPI_Wow64EnableWow64FsRedirection($bEnable)`
A wrapper for the Windows API function to enable or disable file system redirection for 32-bit applications on 64-bit Windows, which is crucial for accessing the correct system folders. 