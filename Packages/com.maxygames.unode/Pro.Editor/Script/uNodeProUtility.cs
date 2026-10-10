using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace MaxyGames.UNode.Editors {
    [InitializeOnLoad]
    public static class uNodeProUtility {
        static uNodeProUtility() {
			uNodeUtility.ProBinding.CallbackGetTrimmedGraph = GetTrimmedGraph;
			uNodeEditorUtility.ProBinding.CallbackFindInNodeBrowser = FindInBrowser;
			uNodeEditorUtility.ProBinding.CallbackShowCSharpPreview = ShowCSharpPreview;
			uNodeEditorUtility.ProBinding.CallbackShowGlobalSearch = ShowGlobalSearch;
			uNodeEditorUtility.ProBinding.CallbackShowNodeBrowser = ShowNodeBrowser;
			uNodeEditorUtility.ProBinding.CallbackShowGraphHierarchy = ShowGraphHierarchy;
			uNodeEditorUtility.ProBinding.CallbackShowBookmarks = ShowBookmarks;
		}

		private static void ShowBookmarks() {
			BookmarkWindow.ShowWindow();
		}

		private static void ShowCSharpPreview() {
			RealtimePreviewSourceWindow.ShowWindow();
		}

		private static void ShowGraphHierarchy() {
			GraphHierarchy.ShowWindow();
		}

		private static void FindInBrowser(MemberInfo info) {
			var win = NodeBrowserWindow.ShowWindow();
			win.browser.RevealItem(info);
			win.Focus();
		}

		private static void ShowGlobalSearch() {
			GlobalSearch.ShowWindow();
		}


		private static void ShowNodeBrowser() {
			NodeBrowserWindow.ShowWindow();
		}

		private static Graph GetTrimmedGraph(Graph graph) {
#if UNODE_TRIM_ON_BUILD
			var tempGraph = graph;
			var unityObject = tempGraph.graphContainer as UnityEngine.Object;
			if(unityObject != null && UnityEditor.EditorUtility.IsPersistent(unityObject)) {
				uNodeUtility.trimmedObjects.Add(unityObject);

#if UNODE_TRIM_AGGRESSIVE
				if(graph.graphContainer is IMacroGraph) {
					return new Graph();
				}
#endif

				tempGraph = SerializedGraph.Deserialize(SerializedGraph.Serialize(graph, OdinSerializer.DataFormat.Binary));
				var containers = tempGraph.GetObjectsInChildren<NodeContainer>(true, true).ToArray();
				foreach(var element in containers) {
					while(element.childCount > 0) {
						element.GetChild(0).Destroy();
					}
				}

#if UNODE_TRIM_AGGRESSIVE
				foreach(var element in tempGraph.GetObjectsInChildren(true, true).ToArray()) {
					try {
						element.comment = null;
						if(element is Variable variable) {
							if(variable.modifier.isPrivate) {
								variable.Destroy();
								continue;
							}
							variable.attributes = null;
						}
						else if(element is Property property) {
							if(property.modifier.isPrivate) {
								property.Destroy();
								continue;
							}
							property.attributes = null;
							property.fieldAttributes = null;
							property.getterAttributes = null;
							property.setterAttributes = null;
						}
						else if(element is Function function) {
							if(function.modifier.isPrivate) {
								function.Destroy();
								continue;
							}
							function.attributes = null;
							function.genericParameters = null;
						}
					}
					catch(Exception ex) {
						Debug.LogException(ex);
					}
				}
#endif
			}
			return tempGraph;
#else
				return graph;
#endif
		}
	}
}