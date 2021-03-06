using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AW
{
    /*
     * An Arcweave project. With all the references inside.
     * Contains:
     *  - Components
     *  - Connections
     *  - Elements
     *  - Boards
     *  - Notes (which are, at the moment, ignored)
     */
    [Serializable]
    public class Project : ScriptableObject
    {    
        [Header("The Arcweave Data")]
        public string project;
        public Attribute[] attributes;
        public IComponentEntry[] components;
        public Element[] elements;
        public Connection[] connections;
        public Note[] notes;
        public Board[] boards;
        public BoardFolder[] boardFolders;

        [Space(15.0f)]
        [Header("User Preferences")]
        public int startingBoardIdx = 0;

        [Space(15.0f)]
        [Header("Editor Preferences")]
        public string folderPath = null;

        // Timestamp of Arcweave Project JSON that generated this
        [HideInInspector] public long sourceTimestamp;

        /*
         * Re-establish links inside Entities from serialized IDs.
         */
        private void OnEnable()
        {
            // 'Cause OnEnable is called right after empty object was created too.
            if (project != null)
                Relink();
        }

        /*
         * A method to re-establish links of non-ScriptableObjects members.
         * (components, connections, boards)
         *
         * A regular C# class member, like Element, gets serialized.
         * If the member is referenced by both Board and Project, upon deserialization,
         * two copies of the same member are created.
         *
         * So instead of serializing them as they are, we'll serialize IDs, 
         * and reestablish the links after deserialization.
         */
        public void Relink()
        {
            // Handle components
            for (int i = 0; i < components.Length; i++) {
                if (!(components[i] is Component))
                    continue;

                Component c = components[i] as Component;
                c.RelinkAttributes(this);
            }

            // Handle connections
            for (int i = 0; i < elements.Length; i++)
                elements[i].ResetConnections();
            for (int i = 0; i < connections.Length; i++)
                connections[i].Relink(this);

            // Handle boards
            for (int i = 0; i < boards.Length; i++)
                boards[i].Relink(this);
        }

        /*
         * Get board with given id.
         */
        public Board GetBoard(string id)
        {
            for (int i = 0; i < boards.Length; i++) {
                if (boards[i].id == id)
                    return boards[i] as Board;
            }

            Debug.LogWarning("[Arcweave] Cannot find board with given id: " + id);
            return null;
        }

        /*
         * Get board with given name.
         */
        public Board GetBoardByName(string boardName)
        {
            for (int i = 0; i < boards.Length; i++) {
                if (boards[i].realName == boardName)
                    return boards[i] as Board;
            }
            
            Debug.LogWarning("[Arcweave] Cannot find board with given name: " + boardName);
            return null;
        }

        /*
         * Get all boards. (used to exclude folders which are stored in the same array)
         */
        public List<Board> GetAllBoards()
        {
            List<Board> subset = new List<Board>();
            for (int i = 0; i < boards.Length; i++) {
                if (boards[i] is Board)
                    subset.Add(boards[i] as Board);
            }
            return subset;
        }

        /*
         * Get attribute.
         */
        public Attribute GetAttribute(string id)
        {
            for (int aIdx = 0; aIdx < attributes.Length; aIdx++) {
                if (attributes[aIdx].id == id) {
                    return attributes[aIdx] as Attribute;
                }
            }

            return null;
        }

        /*
         * Get component.
         */
        public Component GetComponent(string id)
        {
            for (int cIdx = 0; cIdx < components.Length; cIdx++) {
                if (components[cIdx].id == id) {
                    return components[cIdx] as Component;
                }
            }

            return null;
        }

        /*
         * Get element by id.
         */
        public Element GetElement(string id)
        {
            for (int eIdx = 0; eIdx < elements.Length; eIdx++) {
                if (elements[eIdx].id == id) {
                    return elements[eIdx];
                }
            }

            return null;
        }

        /*
         * Get note by id.
         */
        public Note GetNote(string id)
        {
            for (int nIdx = 0; nIdx < notes.Length; nIdx++) {
                if (notes[nIdx].id == id) {
                    return notes[nIdx];
                }
            }

            return null;
        }
    } // class Project
} // namespace AW
