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
	[FilePath(uNodePreference.preferenceDirectory + "/Bookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
	public class BookmarkSingleton : ScriptableSingleton<BookmarkSingleton> {
		public BookmarkGroup bookmark = new();

		//public static string FilePath = uNodePreference.preferenceDirectory + "/Bookmarks.asset";
		//[SerializeField]
		//private OdinSerializedData data;

		//public void OnAfterDeserialize() {
		//	if(data != null) {
		//		bookmarks = SerializerUtility.Deserialize<List<Bookmark>>(data);
		//	}
		//	if(bookmarks == null) {
		//		bookmarks = new();
		//	}
		//}

		//public void OnBeforeSerialize() {
		//	data = SerializerUtility.SerializeValue(bookmarks);
		//}

		public void Save() {
			Save(true);
		}
	}

	public class USerializableSingleton<T> where T : class, new() {
		protected static T s_Instance;

		public static T instance {
			get {
				if(s_Instance == null)
					s_Instance = CreateAndLoad();

				return s_Instance;
			}
		}

		// On domain reload ScriptableObject objects gets reconstructed from a backup. We therefore set the s_Instance here
		protected USerializableSingleton() {
			if(s_Instance != null) {
				Debug.LogError("ScriptableSingleton already exists. Did you query the singleton in a constructor?");
			}
			else {
				object casted = this;
				s_Instance = casted as T;
			}
		}

		private static T CreateAndLoad() {
			// Load
			string filePath = GetFilePath();
			if(!string.IsNullOrEmpty(filePath)) {
				var obj = uNodeEditor.EditorDataSerializer.Load<T>(filePath);
				if(obj != null) {
					return obj;
				}
			}
			return new T();
		}

		protected virtual void DoSave() {
			if(s_Instance == null) {
				Debug.LogError("Cannot save Singleton: no instance!");
				return;
			}

			string filePath = GetFilePath();
			if(!string.IsNullOrEmpty(filePath)) {
				string folderPath = Path.GetDirectoryName(filePath);
				if(!Directory.Exists(folderPath))
					Directory.CreateDirectory(folderPath);

				uNodeEditor.EditorDataSerializer.Save(this, filePath);
			}
			else {
				Debug.LogWarning($"Saving has no effect. Your class '{GetType()}' is missing the FilePath field or property. Create static field/property named `FilePath` to specify where to save your Singleton.\nOnly call Save() and use this if you want your state to survive between sessions of Unity.");
			}
		}

		protected static string GetFilePath() {
			Type type = typeof(T);
			var member = type.GetMemberCached("FilePath");
			if(member is FieldInfo) {
				return (member as FieldInfo).GetValueOptimized(null) as string;
			}
			else if(member is PropertyInfo) {
				return (member as PropertyInfo).GetValueOptimized(null) as string;
			}
			return string.Empty;
		}
	}

}
