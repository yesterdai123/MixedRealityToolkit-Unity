﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.LeapMotion
{
    /// <summary>
    /// Class that checks if the Leap Motion Core assets are present and configures the project if they are.
    /// </summary>
    [InitializeOnLoad]
    static class LeapMotionConfigurationChecker
    {
        // The presence of the LeapXRServiceProvider.cs is used to determine if the Leap Motion Core Assets are in the project.
        private const string trackedLeapFileName = "LeapXRServiceProvider.cs";
        private static readonly string[] Definitions = { "LEAPMOTIONCORE_PRESENT" };

        // True if the Leap Motion Core Assets are in the project.
        private static bool isLeapInProject = false;

        // The current supported Leap Core Assets version numbers.
        private static string[] leapCoreAssetsVersionsSupported = new string[] { "4.4.0", "4.5.0" };

        // The current Leap Core Assets version in this project
        private static string currentLeapCoreAssetsVersion = "";

        // The path difference between the root of assets and the root of the Leap Motion Core Assets.
        private static string pathDifference = "";

        // Array of paths to Leap Motion testing directories that will be removed from the project.
        // Make sure each test directory ends with '/'
        // These paths only need to be deleted if the Leap Core Assets version is 4.4.0
        private static readonly string[] pathsToDelete = new string[]
        {
            "LeapMotion/Core/Editor/Tests/",
            "LeapMotion/Core/Plugins/LeapCSharp/Editor/Tests/",
            "LeapMotion/Core/Scripts/Algorithms/Editor/Tests/",
            "LeapMotion/Core/Scripts/DataStructures/Editor/Tests/",
            "LeapMotion/Core/Scripts/Encoding/Editor/",
            "LeapMotion/Core/Scripts/Query/Editor/",
            "LeapMotion/Core/Scripts/Utils/Editor/BitConverterNonAllocTests.cs",
            "LeapMotion/Core/Scripts/Utils/Editor/ListAndArrayExtensionTests.cs",
            "LeapMotion/Core/Scripts/Utils/Editor/TransformUtilTests.cs",
            "LeapMotion/Core/Scripts/Utils/Editor/UtilsTests.cs",
        };

        // Dictionary of names and references of new asmdefs that will be added to the Leap Motion Core Assets.
        private static readonly Dictionary<string, string[]> leapEditorDirectories = new Dictionary<string, string[]>
        {
            { "LeapMotion.Core.Editor", new string[] { "LeapMotion" } },
            { "LeapMotion.Core.Scripts.Animation.Editor", new string[] { "LeapMotion", "LeapMotion.Core.Editor", "LeapMotion.Core.Scripts.Utils.Editor" } },
            { "LeapMotion.Core.Scripts.Attachments.Editor", new string[] { "LeapMotion", "LeapMotion.Core.Editor" } },
            { "LeapMotion.Core.Scripts.Attributes.Editor", new string[] { "LeapMotion" } },
            { "LeapMotion.Core.Scripts.DataStructures.Editor", new string[] { "LeapMotion" } },
            { "LeapMotion.Core.Scripts.EditorTools.Editor", new string[] { "LeapMotion", "LeapMotion.Core.Scripts.Utils.Editor" } },
            { "LeapMotion.Core.Scripts.Utils.Editor", new string[] { "LeapMotion", "LeapMotion.Core.Editor" } },
            { "LeapMotion.Core.Scripts.XR.Editor", new string[] { "LeapMotion", "LeapMotion.Core.Editor" } },
            { "LeapMotion.Core.Tests.Editor", new string[] { "LeapMotion" } }
        };

        static LeapMotionConfigurationChecker()
        {
            // Check if leap core is in the project
            isLeapInProject = ReconcileLeapMotionDefine();

            ConfigureLeapMotion(isLeapInProject);
        }

        /// <summary>
        /// Ensures that the appropriate symbolic constant is defined based on the presence of the Leap Motion Core Assets.
        /// </summary>
        /// <returns>If the define was added or the define has already been added, return true</returns>
        private static bool ReconcileLeapMotionDefine()
        {
            FileInfo[] files = FileUtilities.FindFilesInAssets(trackedLeapFileName);

            if (files.Length > 0)
            {
                ScriptUtilities.AppendScriptingDefinitions(BuildTargetGroup.Standalone, Definitions);
                ScriptUtilities.AppendScriptingDefinitions(BuildTargetGroup.WSA, Definitions);
                return true;
            }
            else
            {
                ScriptUtilities.RemoveScriptingDefinitions(BuildTargetGroup.Standalone, Definitions);
                ScriptUtilities.RemoveScriptingDefinitions(BuildTargetGroup.WSA, Definitions);
                return false;
            }
        }

        /// <summary>
        /// Configure the Leap Motion Core assets if they are in the project.  First remove testing folders, add LeapMotion.asmdef at the
        /// root of the core assets, and add the leap editor asmdefs.  If the core assets are not in the project, make sure the reference
        /// in the Microsoft.MixedReality.Toolkit.Providers.LeapMotion.asmdef does not contain a ref to LeapMotion.
        /// </summary>
        /// <param name="isLeapInProject">Bool that determines if the Leap Motion Core assets are in the project</param>
        private static void ConfigureLeapMotion(bool isLeapInProject)
        {
            FileInfo[] leapDataProviderAsmDefFile = FileUtilities.FindFilesInAssets("Microsoft.MixedReality.Toolkit.Providers.LeapMotion.asmdef");

            AssemblyDefinition leapDataProviderAsmDef = AssemblyDefinition.Load(leapDataProviderAsmDefFile[0].FullName);

            List<string> references = leapDataProviderAsmDef.References.ToList();

            if (isLeapInProject && !references.Contains("LeapMotion"))
            {
                // Get the location of the Leap Core Assets relative to the root directory
                pathDifference = GetPathDifference();

                // Make sure the Leap Core Assets imported are version 4.4.0
                bool isLeapCoreAssetsVersionSupported = LeapCoreAssetsVersionSupport();

                if (isLeapCoreAssetsVersionSupported)
                {
                    RemoveTestingFolders();
                    AddAndUpdateAsmDefs();
                    AddLeapEditorAsmDefs();

                    // Refresh the database because tests were removed and 10 asmdefs were added
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogError("MRTK only supports the Leap Motion Core Assets Version 4.4.0 and 4.5.0, the Leap Motion Core Assets imported are not Version 4.4.0 or 4.5.0");
                }
            }

            if (!isLeapInProject && references.Contains("LeapMotion"))
            {
                references.Remove("LeapMotion");
                leapDataProviderAsmDef.References = references.ToArray();
                leapDataProviderAsmDef.Save(leapDataProviderAsmDefFile[0].FullName);
            }
        }

        /// <summary>
        /// Checks if the Leap Motion Core Assets version is supported.
        /// </summary>
        /// <returns>True, if the Leap Motion Core Assets version imported is supported</returns>
        private static bool LeapCoreAssetsVersionSupport()
        {
            string versionLeapPath = Path.Combine(Application.dataPath, pathDifference, "LeapMotion", "Core", "Version.txt");

            using (StreamReader streamReader = new StreamReader(versionLeapPath))
            {
                while (streamReader.Peek() > -1)
                {
                    string line = streamReader.ReadLine();

                    foreach (string versionNumberSupported in leapCoreAssetsVersionsSupported)
                    {
                        // If the leap core assets version number is supported
                        if (line.Contains(versionNumberSupported))
                        {
                            currentLeapCoreAssetsVersion = versionNumberSupported;
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// The Leap Core Assets currently contain multiple folders with tests in them.  An issue has been filed in the Unity
        /// Modules repo: https://github.com/leapmotion/UnityModules/issues/1097.  The issue with the multiple test folders is when an 
        /// asmdef is placed at the root of the core assets, each folder containing tests needs another separate asmdef.  This method
        /// is used to avoid adding an additional 8 asmdefs to the project, by removing the folders and files that are tests in the 
        /// Leap Core Assets.
        /// </summary>
        private static void RemoveTestingFolders()
        {
            // If one of the leap test directories exists, then we assume the rest have not been deleted
            if (Directory.Exists(Path.Combine(Application.dataPath, pathDifference, pathsToDelete[0])))
            {
                foreach (string path in pathsToDelete)
                {
                    // Get the full path including the path difference in case the core assets are not imported to the root of the project
                    string fullPath = Path.Combine(Application.dataPath, pathDifference, path);

                    // If we are deleting a specific file, then we also need to remove the meta associated with the file
                    if (File.Exists(fullPath) && fullPath.Contains(".cs"))
                    {
                        // Delete the test files
                        FileUtil.DeleteFileOrDirectory(fullPath);

                        // Also delete the meta files
                        FileUtil.DeleteFileOrDirectory(fullPath + ".meta");
                    }

                    if (Directory.Exists(fullPath))
                    {
                        // Delete the test directories
                        FileUtil.DeleteFileOrDirectory(fullPath);

                        // Delete the test directories meta files
                        FileUtil.DeleteFileOrDirectory(fullPath.TrimEnd('/') + ".meta");
                    }
                }
            }
        }

        /// <summary>
        /// Adds an asmdef at the root of the LeapMotion Core Assets once they are imported into the project and adds the newly created LeapMotion.asmdef
        /// as a reference for the existing leap data provider asmdef.
        /// </summary>
        private static void AddAndUpdateAsmDefs()
        {
            string leapCoreAsmDefPath = Path.Combine(Application.dataPath, pathDifference, "LeapMotion", "LeapMotion.asmdef");

            // If the asmdef has already been created then do not create another one
            if (!File.Exists(leapCoreAsmDefPath))
            {
                // Create the asmdef that will be placed in the Leap Core Assets when they are imported
                // A new asmdef needs to be created in order to reference it in the MRTK/Providers/LeapMotion/Microsoft.MixedReality.Toolkit.Providers.LeapMotion.asmdef file
                AssemblyDefinition leapAsmDef = new AssemblyDefinition
                {
                    Name = "LeapMotion",
                    AllowUnsafeCode = true,
                    References = new string[] { },
                    IncludePlatforms = new string[] { "Editor", "WindowsStandalone32", "WindowsStandalone64" }
                };

                leapAsmDef.Save(leapCoreAsmDefPath);

                // Get the MRTK/Providers/LeapMotion/Microsoft.MixedReality.Toolkit.Providers.LeapMotion.asmdef
                FileInfo[] leapDataProviderAsmDefFile = FileUtilities.FindFilesInAssets("Microsoft.MixedReality.Toolkit.Providers.LeapMotion.asmdef");

                // Add the newly created LeapMotion.asmdef to the references of the leap data provider asmdef
                AssemblyDefinition leapDataProviderAsmDef = AssemblyDefinition.Load(leapDataProviderAsmDefFile[0].FullName);

                List<string> references = leapDataProviderAsmDef.References.ToList();

                if (!references.Contains("LeapMotion"))
                {
                    references.Add("LeapMotion");
                }

                leapDataProviderAsmDef.References = references.ToArray();

                leapDataProviderAsmDef.Save(leapDataProviderAsmDefFile[0].FullName);
            }
        }

        /// <summary>
        /// Add asmdefs to the editor directories in the leap core assets.
        /// </summary>
        private static void AddLeapEditorAsmDefs()
        {
            if (FileUtilities.FindFilesInAssets("LeapMotion.Core.Editor.asmdef").Length == 0)
            {
                foreach (KeyValuePair<string, string[]> leapAsmDef in leapEditorDirectories)
                {
                    // Convert asmdef name to a path
                    string leapAsmDefPath = leapAsmDef.Key.Replace('.', '/');

                    string leapAsmDefFilename = string.Concat(leapAsmDef.Key, ".asmdef");

                    // Path for the asmdef including the filename
                    string fullLeapAsmDefFilePath = Path.Combine(Application.dataPath, pathDifference, leapAsmDefPath, leapAsmDefFilename);

                    // Path for the asmdef NOT including the filename
                    string fullLeapAsmDefDirectoryPath = Path.Combine(Application.dataPath, pathDifference, leapAsmDefPath);

                    // Make sure the directory exists within the leap core assets before we add the asmdef
                    // The leap core assets version 4.5.0 contains the LeapMotion/Core/Tests/Editor directory while 4.4.0 does not.
                    if (!File.Exists(fullLeapAsmDefFilePath) && Directory.Exists(fullLeapAsmDefDirectoryPath))
                    {
                        // Create and save the new asmdef
                        AssemblyDefinition leapEditorAsmDef = new AssemblyDefinition
                        {
                            Name = leapAsmDef.Key,
                            References = leapAsmDef.Value,
                            IncludePlatforms = new string[] { "Editor" }
                        };

#if !UNITY_2019_3_OR_NEWER
                        // In Unity 2018.4, directories that contain tests need to have a test assembly.
                        // An asmdef is added to a leap directory that contains tests for the leap core assets 4.5.0.
                        if (leapEditorAsmDef.Name.Contains("Tests"))
                        {
                            leapEditorAsmDef.OptionalUnityReferences = new string[] { "TestAssemblies" };
                        }
#endif

                        leapEditorAsmDef.Save(fullLeapAsmDefFilePath);
                    }
                }
            }
        }

        /// <summary>
        /// Get the difference between the root of assets and the location of the leap core assets.  If the leap core assets 
        /// are at the root of assets, there is no path difference.
        /// </summary>
        /// <returns>Returns an empty string if the leap core assets are at the root of assets, otherwise return the path difference</returns>
        private static string GetPathDifference()
        {
            // The file LeapXRServiceProvider.cs is used as a location anchor instead of the LeapMotion directory
            // to avoid a potential incorrect location return if there is a folder named LeapMotion prior to the leap 
            // core assets import 
            FileInfo[] leapPathLocationAnchor = FileUtilities.FindFilesInAssets(trackedLeapFileName);
            string leapFilePath = leapPathLocationAnchor[0].FullName;

            List<string> leapPath = leapFilePath.Split(Path.DirectorySeparatorChar).ToList();

            // Remove the last 3 elements of leap path (/Core/Scripts/LeapXRService.cs) from the list to get the root of the leap core assets
            leapPath.RemoveRange(leapPath.Count - 3, 3);

            List<string> unityDataPath = Application.dataPath.Split('/').ToList();
            unityDataPath.Add("LeapMotion");

            // Get the difference between the root of assets and the root of leap core assets
            IEnumerable<string> difference = leapPath.Except(unityDataPath);

            return string.Join("/", difference);
        }

        /// <summary>
        /// Adds warnings to the nowarn line in the csc.rsp file located at the root of assets.  Warning 618 and 649 are added to the nowarn line because if
        /// the MRTK source is from the repo, warnings are converted to errors. Warnings are not converted to errors if the MRTK source is from the unity packages.
        /// Warning 618 and 649 are logged when the Leap Motion Core Assets are imported into the project, 618 is the obsolete warning and 649 is a null on start warning.
        /// </summary>
        [MenuItem("Mixed Reality Toolkit/Utilities/Leap Motion/Configure CSC File for Leap Motion")]
        static void UpdateCSC()
        {
            // The csc file will always be in the root of assets
            string cscFilePath = Path.Combine(Application.dataPath, "csc.rsp");

            // Each line of the csc file
            List<string> cscFileLines = new List<string>();

            // List of the warning numbers after "-nowarn: " in the csc file
            List<string> warningNumbers = new List<string>();

            // List of new warning numbers to add to the csc file
            List<string> warningNumbersToAdd = new List<string>()
            {
                "618",
                "649"
            };

            using (StreamReader streamReader = new StreamReader(cscFilePath))
            {
                while (streamReader.Peek() > -1)
                {
                    string cscFileLine = streamReader.ReadLine();

                    if (cscFileLine.Contains("-nowarn"))
                    {
                        string[] currentWarningNumbers = cscFileLine.Split(',', ':');
                        warningNumbers = currentWarningNumbers.ToList();

                        // Remove "nowarn" from the warningNumbers list
                        warningNumbers.Remove("-nowarn");

                        foreach (string warningNumberToAdd in warningNumbersToAdd)
                        {
                            // Add the new warning numbers if they are not already in the file
                            if (!warningNumbers.Contains(warningNumberToAdd))
                            {
                                warningNumbers.Add(warningNumberToAdd);
                            }
                        }

                        cscFileLines.Add(string.Join(",", warningNumbers));
                    }
                    else
                    {
                        cscFileLines.Add(cscFileLine);
                    }
                }
            }

            using (StreamWriter streamWriter = new StreamWriter(cscFilePath))
            {
                foreach (string cscLine in cscFileLines)
                {
                    if (cscLine.StartsWith("1701"))
                    {
                        string warningNumbersJoined = string.Join(",", warningNumbers);
                        streamWriter.WriteLine(string.Concat("-nowarn:", warningNumbersJoined));
                    }
                    else
                    {
                        streamWriter.WriteLine(cscLine);
                    }
                }
            }

            Debug.Log($"Saving {cscFilePath}");
        }

#if UNITY_2018       
        /// <summary>
        /// Force Leap Motion integration after the Leap Motion Core Assets import.  In Unity 2018.4, the configuration checker sometimes does not update after the 
        /// Leap Motion Core Assets import, this case only occurs if the MRTK source is from the unity packages. If the integration of leap and MRTK has not occurred, users can 
        /// select the Configure Leap Motion menu option to force integration. 
        /// </summary>
        [MenuItem("Mixed Reality Toolkit/Utilities/Leap Motion/Configure Leap Motion")]
        static void ForceLeapMotionConfiguration()
        {
            // Check if leap core is in the project
            isLeapInProject = ReconcileLeapMotionDefine();

            ConfigureLeapMotion(isLeapInProject);
        }
#endif
    }
}

