using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace AW.Editor
{
    /*
     * A collection of static utilities for managing Boards.
     */
    public static class ProjectUtils
    {
        // Constant
        public const string projectFileName = "project_settings.json";
        public const string projectResourceFolder = "/Resources/Arcweave/";

        /*
         *
         */
        public static Project FetchProject()
        {
            string projectPath = "Assets" + ProjectUtils.projectResourceFolder + "Project.asset";
            return AssetDatabase.LoadAssetAtPath<Project>(projectPath) as Project;
        }

        /*
         * Get project file at given path.
         */
        public static FileInfo GetProjectFile(string path)
        {
            string[] filePaths = Directory.GetFiles(path);

            FileInfo projectFileInfo = null;
            for (int i = 0; i < filePaths.Length; i++) {
                FileInfo fi = new FileInfo(filePaths[i]);
                if (fi.Name == projectFileName) {
                    // We're good, correct project path
                    projectFileInfo = fi;
                    return projectFileInfo;
                }
            }

            return null;
        }

        /*
         * Validate target project folder.
         */
        public static bool IsProjectFolderEmpty()
        {
            string resPath = Application.dataPath + projectResourceFolder;

            if (!Directory.Exists(resPath)) {
                CreateProjectFolders();
                return true;
            } else {
                // Check if there are any files in the directory
                string[] filePaths = Directory.GetFiles(resPath);
                return filePaths.Length == 0;
            }
        }

        /*
         * Create project folders.
         */
        public static void CreateProjectFolders()
        {
            string resPath = Application.dataPath + projectResourceFolder;
            Directory.CreateDirectory(resPath);

            // Create other folders
            Directory.CreateDirectory(resPath + "/Components");
            Directory.CreateDirectory(resPath + "/Boards");
        }

        /*
         * Read project
         */
        public static bool ReadProject(Project project, string projectFolder)
        {
            // Asset paths are relative to project folder
            string unityPrjFolder = Application.dataPath.Replace("Assets", "");
            projectFolder = projectFolder.Replace(unityPrjFolder, "");

            // Ensure the target folder is empty, and exists.
            if (!IsProjectFolderEmpty()) {
                ClearProjectFolder();
                CreateProjectFolders();
            }

            try {
                string fullPrjPath = projectFolder + "/" + projectFileName;
                TextAsset projectAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(fullPrjPath);
                if (projectAsset == null)
                    throw new Exception("No project asset found at " + fullPrjPath + ".");

                string projectContents = Encoding.UTF8.GetString(projectAsset.bytes);
                JSONNode root = JSONNode.Parse(projectContents);

                project.project = root["name"];

                // Read attributes
                EditorUtility.DisplayProgressBar("Arcweave", "Creating attributes...", 0.0f);
                ReadAttributes(project, root["attributes"].AsObject);

                // Read components
                EditorUtility.DisplayProgressBar("Arcweave", "Creating components...", 15.0f);
                ReadComponents(project, root["components"].AsObject, projectFolder);

                // Read elements
                EditorUtility.DisplayProgressBar("Arcweave", "Creating elements...", 30.0f);
                ReadElements(project, root["elements"].AsObject);

                // Read connections
                EditorUtility.DisplayProgressBar("Arcweave", "Creating connections...", 45.0f);
                ReadConnections(project, root["connections"].AsObject);

                // Read notes
                EditorUtility.DisplayProgressBar("Arcweave", "Creating notes...", 60.0f);
                ReadNotes(project, root["notes"].AsObject);

                // Read boards
                EditorUtility.DisplayProgressBar("Arcweave", "Creating boards...", 75.0f);
                BoardUtils.ReadBoards(project, root["boards"].AsObject);

                // Re-do the entity linking
                project.Relink();

                // Resolve references inside elements and connections
                // Must happen after reading everything, so Arcweave has the referenced elements instantiated.
                EditorUtility.DisplayProgressBar("Arcweave", "Preprocessing HTML...", 90.0f);
                for (int i = 0; i < project.elements.Length; i++) {
                    project.elements[i].ParseHTML(project);
                }

                for (int i = 0; i < project.connections.Length; i++) {
                    project.connections[i].ParseHTML(project);
                }

                // Set default roots for boards
                for (int i = 0; i < project.boards.Length; i++) {
                    BoardUtils.SetDefaultRoot(project.boards[i]);
                    EditorUtility.SetDirty(project.boards[i]);
                }
            } catch (Exception e) {
                Debug.LogError("[Arcweave] Cannot load project: " + e.Message + "\n" + e.StackTrace);
                EditorUtility.ClearProgressBar();
                return false;
            }

            EditorUtility.ClearProgressBar();
            return true;
        }

        /*
         * Read attributes from given JSON Class.
         */
        private static void ReadAttributes(Project project, JSONClass attributesRoot)
        {
            List<Attribute> tmp = new List<Attribute>();

            IEnumerator children = attributesRoot.GetEnumerator();
            while (children.MoveNext()) {
                // Get current
                KeyValuePair<string, JSONNode> current = (children.Current != null) ?
                    (KeyValuePair<string, JSONNode>)children.Current : default(KeyValuePair<string, JSONNode>);
                JSONNode child = current.Value;

                string unusedBoardId = null; // ToDo: The HTML parser is shit because I have to pass this useless parameter most of the time
                Attribute a = new Attribute();
                a.id = current.Key;
                Utils.ParseHTML(child["label"], ref a.label, ref a.labelNoStyle, ref unusedBoardId);
                Utils.ParseHTML(child["content"], ref a.content, ref a.contentNoStyle, ref unusedBoardId);
                tmp.Add(a);
            }

            project.attributes = tmp.ToArray();
        }

        /*
         * Read components from given JSON Class.
         */
        private static void ReadComponents(Project project, JSONClass componentRoot, string projectPath)
        {
            string componentPath = "Assets" + projectResourceFolder + "Components/";

            List<IComponentEntry> entries = new List<IComponentEntry>();

            IEnumerator children = componentRoot.GetEnumerator();
            while (children.MoveNext()) {
                // Get current
                KeyValuePair<string, JSONNode> current = (children.Current != null) ?
                    (KeyValuePair<string, JSONNode>)children.Current : default(KeyValuePair<string, JSONNode>);
                JSONNode child = current.Value;

                // Get its ID
                string id = current.Key;
                bool isFolder = child["children"] != null;

                if (isFolder) {
                    ComponentFolder folder = ScriptableObject.CreateInstance<ComponentFolder>();
                    folder.id = id;
                    ReadComponentFolder(folder, child);
                    entries.Add(folder);
                    AssetDatabase.CreateAsset(folder, componentPath + folder.id + ".asset");
                } else {
                    // Async operation because it might load images
                    Component component = ScriptableObject.CreateInstance<Component>();
                    component.id = id;
                    ReadComponent(project, component, child, projectPath);
                    entries.Add(component);
                    AssetDatabase.CreateAsset(component, componentPath + component.id + ".asset");
                }
            }

            project.components = entries.ToArray();
        }

        /*
         * Read component folder from JSON entry.
         */
        private static void ReadComponentFolder(ComponentFolder cf, JSONNode node)
        {
            cf.name = cf.id;
            cf.realName = node["name"];

            JSONArray idxArray = node["children"].AsArray;
            if (idxArray.Count == 0)
                return;

            cf.childIds = new int[idxArray.Count];

            for (int i = 0; i < idxArray.Count; i++)
                cf.childIds[i] = idxArray[i].AsInt;
        }

        /*
         * Read component from JSON entry.
         */
        private static void ReadComponent(Project project, Component c, JSONNode root, string projectPath)
        {
            c.name = c.id;
            c.realName = root["name"];

            // Attempt to load the image
            string imgPath = root["image"];
            if (!string.IsNullOrEmpty(imgPath) && imgPath != "null") {
                // Load sprite at given path
                string fullPath = projectPath + "/assets/" + imgPath;
                c.image = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
                if (c.image == null) {
                    Debug.LogWarning("[Arcweave] Could not load image at path: " + fullPath + " for component " + c.name);
                }
            }

            // Load the attributes
            JSONArray attributeArray = root["attributes"].AsArray;
            c.attributeIDs = new string[attributeArray.Count];;
            for (int i = 0; i < attributeArray.Count; i++) {
                string attributeID = attributeArray[i];
                c.attributeIDs[i] = attributeID;
            }
        }

        /*
         * Read elements from JSON entry.
         */
        private static void ReadElements(Project project, JSONClass elementRoot)
        {
            List<Element> tmp = new List<Element>();

            IEnumerator children = elementRoot.GetEnumerator();
            while (children.MoveNext()) {
                // Get current
                KeyValuePair<string, JSONNode> current = (children.Current != null) ?
                    (KeyValuePair<string, JSONNode>)children.Current : default(KeyValuePair<string, JSONNode>);
                JSONNode child = current.Value;
                
                // Create element
                Element element = new Element();
                element.id = current.Key;
                ReadElement(element, project, current.Value);

                // Add
                tmp.Add(element);
            }

            project.elements = tmp.ToArray();
        }

        /*
         * Read element from node.
         */
        private static void ReadElement(Element e, Project project, JSONNode root)
        {
            // Read & Parse Title
            e.title = root["title"];

            // Read & Parse Content
            e.content = root["content"];

            // Read components
            JSONArray componentArray = root["components"].AsArray;
            e.components = new Component[componentArray.Count];
            for (int i = 0; i < componentArray.Count; i++) {
                string compId = componentArray[i];

                Component c = project.GetComponent(compId);
                
                if (c == null) {
                    Debug.LogWarning("[Arcweave] Cannot find component for given id: " + compId);
                    continue;
                }

                e.components[i] = c;
            }

            // Handle linked board tag
            string linkedBoardID = root["linkedBoard"];
            if (e.linkedBoardId != null) {
                //Debug.LogWarning("[Arcweave] Linked board became available for reading!");
            }
        }

        /*
         * Read connections from JSON entry.
         */
        private static void ReadConnections(Project project, JSONClass root)
        {
            List<Connection> tmp = new List<Connection>();

            IEnumerator children = root.GetEnumerator();
            while (children.MoveNext()) {
                // Get current
                KeyValuePair<string, JSONNode> current = (children.Current != null) ?
                    (KeyValuePair<string, JSONNode>)children.Current : default(KeyValuePair<string, JSONNode>);
                JSONNode child = current.Value;

                // Create element
                Connection c = new Connection();
                c.id = current.Key;
                c.label = child["label"];
                c.sourceElementId = child["sourceid"];
                c.targetElementId = child["targetid"];

                // Add
                tmp.Add(c);
            }

            project.connections = tmp.ToArray();
        }

        /*
         * Read notes from JSON entry.
         */
        private static void ReadNotes(Project project, JSONClass noteRoot)
        {
            List<Note> tmp = new List<Note>();            

            IEnumerator children = noteRoot.GetEnumerator();
            while (children.MoveNext()) {
                // Get current
                KeyValuePair<string, JSONNode> current = (children.Current != null) ?
                    (KeyValuePair<string, JSONNode>)children.Current : default(KeyValuePair<string, JSONNode>);
                JSONNode child = current.Value;
                
                // Create element
                Note note = new Note(current.Key, current.Value);

                // Add
                tmp.Add(note);
            }

            project.notes = tmp.ToArray();
        }

        /*
         * Destroy project
         */
        public static void DestroyProject(Project project)
        {
            for (int i = 0; i < project.components.Length; i++) {
                string path = AssetDatabase.GetAssetPath(project.components[i]);
                AssetDatabase.DeleteAsset(path);
            }

            for (int i = 0; i < project.boards.Length; i++) {
                string path = AssetDatabase.GetAssetPath(project.boards[i]);
                AssetDatabase.DeleteAsset(path);
            }

            for (int i = 0; i < project.boardFolders.Length; i++) {
                string path = AssetDatabase.GetAssetPath(project.boardFolders[i]);
                AssetDatabase.DeleteAsset(path);
            }

            string pPath = AssetDatabase.GetAssetPath(project);
            AssetDatabase.DeleteAsset(pPath);
        }

        /*
         * Clear project classes.
         */
        public static void ClearProjectFolder()
        {
            try {
                string resPath = Application.dataPath + projectResourceFolder;
                if (Directory.Exists(resPath))
                    Directory.Delete(resPath, true);
            } catch (Exception e) {
                Debug.LogWarning("[Arcweave] Could not properly clean up project:\n" + e.Message + "\n" + e.StackTrace);
            }
        }
    } // ProjectUtils
} // AW.Editor
