using System;
using System.Collections.Generic;
using UnityEngine;
using MaxyGames.UNode.Nodes;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using System.IO;
using System.Reflection;

namespace MaxyGames.UNode.Editors {

	public class BookmarkNodeCommand : NodeMenuCommand {
		public override string name => "Add node to bookmark";

		public override void OnClick(Node source, Vector2 mousePosition) {
			var value = new BookmarkElement() {
				name = "newBookmark",
				element = new(source),
			};
			ActionWindow.Show(() => {
				value.name = EditorGUILayout.TextField("name", value.name);
				EditorGUILayout.PrefixLabel("summary");
				value.summary = EditorGUILayout.TextArea(value.summary);
				value.icon = EditorGUILayout.ObjectField("Icon", value.icon, typeof(Texture2D), false) as Texture2D;
			}, onGUIBottom: () => {
				if(GUILayout.Button("Add")) {
					BookmarkManager.AddBookmark(value);
					ActionWindow.CloseAll();
				}
			});
		}

		public override bool IsValidNode(Node source) {
			return graphData.selectedCount == 1;
		}
	}

	public class BookmarkGraphCommand : GraphMenuCommand {
		public override string name {
			get {
				return "Add Bookmark";
			}
		}

		public override void OnClick(Vector2 mousePosition) {
			var value = new BookmarkCanvas() {
				name = "newBookmark",
				position = graphData.position,
				zoomScale = graphEditor.zoomScale,
				canvas = new(graphData.currentCanvas),
			};
			ActionWindow.Show(() => {
				value.name = EditorGUILayout.TextField("name", value.name);
				EditorGUILayout.PrefixLabel("summary");
				value.summary = EditorGUILayout.TextArea(value.summary);
				value.icon = EditorGUILayout.ObjectField("Icon", value.icon, typeof(Texture2D), false) as Texture2D;
			}, onGUIBottom: () => {
				if(GUILayout.Button("Add")) {
					BookmarkManager.AddBookmark(value);
					ActionWindow.CloseAll();
				}
			});
		}

		public override bool IsValid() {
			return EditorUtility.IsPersistent(graphData.owner);
		}
	}
}
