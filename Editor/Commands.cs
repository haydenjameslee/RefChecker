﻿using System;
using System.Reflection;
using UnityRefCheckerExternal;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityRefChecker
{
    public static class Commands
    {
        private static bool wasErrorInCheck = false;
        private static bool runningAfterCompilation = false;

        [DidReloadScripts]
        private static void RunAfterCompilation() {
            if (Settings.GetCheckOnCompilation()) {
                runningAfterCompilation = true;
                CheckBuildScenes();
                runningAfterCompilation = false;
            }
        }

        public static void CheckBuildScenes() {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            string previouslyOpenScenePath = SceneManager.GetActiveScene().path;

            EditorBuildSettingsScene[] buildSettingsScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildSettingsScenes.Length; i++) {
                EditorBuildSettingsScene settingsScene = buildSettingsScenes[i];
                Scene scene = GetSceneFromSettingsScene(settingsScene);
                CheckScene(scene);
            }
            EditorSceneManager.OpenScene(previouslyOpenScenePath);

            if (!wasErrorInCheck && !runningAfterCompilation) {
                Debug.Log("UnityRefChecker: All good!");
            }
            wasErrorInCheck = false;
        }

        public static void CheckOpenScene() {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var scene = SceneManager.GetActiveScene();
            CheckScene(scene);

            if (!wasErrorInCheck && !runningAfterCompilation) {
                Debug.Log("UnityRefChecker: All good!");
            }
            wasErrorInCheck = false;
        }

        public static void ClearConsole() {
            Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
            Type logEntries = assembly.GetType("UnityEditorInternal.LogEntries");
            MethodInfo clearConsoleMethod = logEntries.GetMethod("Clear");
            clearConsoleMethod.Invoke(new object(), null);
        }

        private static Scene GetSceneFromSettingsScene(EditorBuildSettingsScene settingsScene) {
            string scenePath = settingsScene.path;
            EditorSceneManager.OpenScene(scenePath);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            return scene;
        }

        private static void CheckScene(Scene scene) {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) {
                CheckRootGameObject(roots[i]);
            }
        }

        private static void CheckRootGameObject(GameObject go) {
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++) {
                CheckComponent(components[i]);
            }
        }

        private static void CheckComponent(Component c) {
            // Ignore non-MonoBehaviours like Transform, Camera etc
            bool isBehaviour = c as MonoBehaviour;
            if (!isBehaviour) {
                return;
            }

            Type compType = c.GetType();
            BindingFlags fieldTypes = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            FieldInfo[] fields = compType.GetFields(fieldTypes);

            for (int i = 0; i < fields.Length; i++) {
                FieldInfo info = fields[i];

                //Debug.Log("Field=" + info.Name + " type=" + info.MemberType);
                bool shouldPrintLog = ShouldPrintLogForComponent(c, info);

                if (shouldPrintLog) {
                    BuildAndPrintLog(c, info);

                    if (!wasErrorInCheck) {
                        wasErrorInCheck = true;
                    }
                }
            }
        }

        private static bool ShouldPrintLogForComponent(Component c, FieldInfo info) {
            object value = info.GetValue(c);
            bool isAssigned = value != null;

            bool hasIgnoreAttribute = FieldHasAttribute(info, typeof(IgnoreRefCheckerAttribute));

            bool isSerializeable = info.IsPublic || FieldHasAttribute(info, typeof(SerializeField));
            bool hiddenInInspector = FieldHasAttribute(info, typeof(HideInInspector));

            bool shouldPrintLog = !isAssigned && !hasIgnoreAttribute && isSerializeable && !hiddenInInspector;
            return shouldPrintLog;
        }

        private static bool FieldHasAttribute(FieldInfo info, Type attributeType) {
            return info.GetCustomAttributes(attributeType, true).Length > 0;
        }

        private static string BuildLog(Component c, FieldInfo info) {
            var log = new ColorfulLogBuilder();
            bool useColor = Settings.GetColorfulLogs();
            log.SetColorful(useColor);
            log.Append("UnityRefChecker: Component ");
            log.StartColor();
            log.Append(c.GetType().Name);
            log.EndColor();
            log.Append(" has a null reference for field ");
            log.StartColor();
            log.Append(info.Name);
            log.EndColor();
            log.Append(" on GameObject ");
            log.StartColor();
            log.Append(c.gameObject.name);
            log.EndColor();
            log.Append(" in Scene ");
            log.StartColor();
            log.Append(c.gameObject.scene.name);
            log.EndColor();
            return log.ToString();
        }

        private static void BuildAndPrintLog(Component c, FieldInfo info) {
            string log = BuildLog(c, info);
            Debug.logger.LogFormat(Settings.GetLogSeverity(), log);
        }
    }
}