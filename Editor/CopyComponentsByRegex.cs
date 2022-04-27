﻿namespace CopyComponentsByRegex {
	using System.Collections.Generic;
	using System.Collections;
	using System.Linq;
	using System.Text.RegularExpressions;
	using UnityEditor;
	using UnityEngine;

	[System.Serializable]
	class TreeItem {
		public string name;
		public string type;
		public GameObject gameObject;
		public List<TreeItem> children;
		public List<Component> components;
		public TreeItem (GameObject go) {
			name = go.name;
			type = go.GetType ().ToString ();
			gameObject = go;
			components = new List<Component> ();
			children = new List<TreeItem> ();
		}
	}

	public class CopyComponentsByRegexWindow : EditorWindow {
		static GameObject activeObject;
		static string pattern = "";
		static TreeItem copyTree = null;
		static Transform root = null;
		static List<Transform> transforms = null;
		static List<Component> components = null;
		static bool isRemoveBeforeCopy = false;
		static bool isObjectCopy = false;
		static bool isObjectCopySaveTransform = false;
		static bool isClothNNS = false;
		static bool copyTransform = false;
		static bool pasteValuesIfExists = false;

		void OnEnable () {
			pattern = EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/pattern") ?? "";
			isRemoveBeforeCopy = bool.Parse (EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/isRemoveBeforeCopy") ?? isRemoveBeforeCopy.ToString ());
			isObjectCopy = bool.Parse (EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/isObjectCopy") ?? isObjectCopy.ToString ());
			isObjectCopySaveTransform = bool.Parse (EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/isObjectCopySaveTransform") ?? isObjectCopySaveTransform.ToString ());
			isClothNNS = bool.Parse (EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/isClothNNS") ?? isClothNNS.ToString ());
			copyTransform = bool.Parse (EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/copyTransform") ?? copyTransform.ToString ());
			pasteValuesIfExists = bool.Parse (EditorUserSettings.GetConfigValue ("CopyComponentsByRegex/pasteValuesIfExists") ?? pasteValuesIfExists.ToString ());
		}

		void OnSelectionChange () {
			var editorEvent = EditorGUIUtility.CommandEvent ("ChangeActiveObject");
			editorEvent.type = EventType.Used;
			SendEvent (editorEvent);
		}

		// Use this for initialization
		[MenuItem ("GameObject/Copy Components By Regex", false, 20)]
		public static void ShowWindow () {
			activeObject = Selection.activeGameObject;
			EditorWindow.GetWindow (typeof (CopyComponentsByRegexWindow));
		}

		static void CopyWalkdown (GameObject go, ref TreeItem tree, ref Regex regex, int depth = 0) {
			transforms.Add (go.transform);

			// Components
			foreach (Component component in go.GetComponents<Component> ()) {
				if (component == null || !regex.Match (component.GetType ().ToString ()).Success) {
					continue;
				}
				tree.components.Add (component);
			}

			// Children
			var children = GetChildren (go);
			foreach (Transform child in children) {
				var node = new TreeItem (child.gameObject);
				tree.children.Add (node);
				CopyWalkdown (child.gameObject, ref node, ref regex, depth + 1);
			}
		}

		static List<TreeItem> SearchRoute (Transform root, Transform dst) {
			List<TreeItem> down = new List<TreeItem> ();

			if (root == dst) {
				return down;
			}

			var current = dst;
			while (CopyComponentsByRegexWindow.root != current) {
				down.Add (new TreeItem (current.gameObject));
				current = current.parent;
				if (current == null) {
					return null;
				}
			}
			down.Reverse ();

			return down;
		}

		static void MergeWalkdown (GameObject go, ref TreeItem tree, int depth = 0) {
			if (depth > 0 && go.name != tree.name) {
				return;
			}

			var targetComponents = go.GetComponents<Component> ();
			Dictionary<System.Type, int> currentComponentCount = new Dictionary<System.Type, int> ();
			
			// copy components
			foreach (Component component in tree.components) {
				UnityEditorInternal.ComponentUtility.CopyComponent (component);

				if (component is Cloth) {
					var cloth = go.GetComponent<Cloth> () == null ? go.AddComponent<Cloth> () : go.GetComponent<Cloth> ();
					CopyProperties (component, cloth);

					Component dstComponent = cloth;
					var srcCloth = (component as Cloth);
					var dstCloth = (dstComponent as Cloth);
					var srcCoefficients = srcCloth.coefficients;
					var dstCoefficients = dstCloth.coefficients;

					if (isClothNNS) {
						var srcVertices = srcCloth.vertices;
						var dstVertives = dstCloth.vertices;

						// build KD-Tree
						var kdtree = new KDTree (
							srcVertices,
							0,
							(srcVertices.Length < srcCoefficients.Length ? srcVertices.Length : srcCoefficients.Length) - 1
						);

						for (int i = 0, il = dstCoefficients.Length, ml = dstVertives.Length; i < il && i < ml; ++i) {
							var srcIdx = kdtree.FindNearest (dstVertives[i]);
							dstCoefficients[i].collisionSphereDistance = srcCoefficients[srcIdx].collisionSphereDistance;
							dstCoefficients[i].maxDistance = srcCoefficients[srcIdx].maxDistance;
						}
						dstCloth.coefficients = dstCoefficients;
					} else {
						if (srcCoefficients.Length == dstCoefficients.Length) {
							for (int i = 0, il = srcCoefficients.Length; i < il; ++i) {
								dstCoefficients[i].collisionSphereDistance = srcCoefficients[i].collisionSphereDistance;
								dstCoefficients[i].maxDistance = srcCoefficients[i].maxDistance;
							}
							dstCloth.coefficients = dstCoefficients;
						}
					}
				} else if (component is Transform) {
					Component dstComponent = go.GetComponent<Transform>();
					if (copyTransform) {
						UnityEditorInternal.ComponentUtility.PasteComponentValues (dstComponent);
					}
				} else {
					if (!pasteValuesIfExists) {
						UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
					} else {
						// https://gist.github.com/tsubaki/d049957ad312e3a12764
						// 同じコンポーネントが複数ある場合、上から並んでいる順番にコピーする(一部削除されている場合はズレる)
						var componentCount = targetComponents.Count (c => c.GetType () == component.GetType ());
						if (componentCount == 0) {
							UnityEditorInternal.ComponentUtility.PasteComponentAsNew (go);
						} else if (componentCount == 1) {
							var targetComponent = targetComponents.First (c => c.GetType () == component.GetType ());
							UnityEditorInternal.ComponentUtility.PasteComponentValues (targetComponent);
						} else {
							if (currentComponentCount.ContainsKey(component.GetType()) == false) {
								currentComponentCount.Add(component.GetType(), 0);
							}

							var count = currentComponentCount[component.GetType()];
							var targetComponentsWithType =
								targetComponents.Where(c => c.GetType() == component.GetType());
							if (count < targetComponentsWithType.Count()) {
								var targetComponent = targetComponents.Where(c => c.GetType() == component.GetType())
									.ElementAt(count);
								currentComponentCount[component.GetType()] += 1;
								UnityEditorInternal.ComponentUtility.PasteComponentValues(targetComponent);
							} else {
								UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
							}
						}
					}
				}
			}
			
			foreach (Component c in go.GetComponents<Component>()) {
				components.Add(c);
			}
			
			// children
			var children = GetChildren (go);
			var childDic = new Dictionary<string, Transform> ();
			foreach (Transform child in children) {
				childDic[child.gameObject.name] = child;
			}
			foreach (TreeItem treeChild in tree.children) {
				Transform child;
				var next = treeChild;
				if (!childDic.ContainsKey (treeChild.name)) {
					if (!isObjectCopy) {
						continue;
					}
					GameObject childObject;
					if (isObjectCopySaveTransform) {
						// Quaternionが難しいのでTransformで処理。rootのlocalPositionをコピー先でも維持する。
						childObject = (GameObject) Object.Instantiate (treeChild.gameObject, root, true);
						child = childObject.transform;
						var localPosition = child.localPosition;
						var localRotation = child.localRotation;
						child.parent = activeObject.transform;
						child.localPosition = localPosition;
						child.localRotation = localRotation;
						child.parent = go.transform;
					} else {
						childObject = (GameObject) Object.Instantiate (treeChild.gameObject, go.transform);
						child = childObject.transform;
					}
					childObject.name = treeChild.name;

					// コピーしたオブジェクトに対しては自動的に同種コンポーネントの削除を行う
					if (isRemoveBeforeCopy || !pasteValuesIfExists) {
						RemoveWalkdown(childObject, ref next);
					}
				} else {
					child = childDic[treeChild.name];
				}

				if (child.gameObject.GetType ().ToString () == treeChild.type) {
					MergeWalkdown (child.gameObject, ref next, depth + 1);
				}
			}
		}

		static void RemoveWalkdown (GameObject go, ref TreeItem tree, int depth = 0) {
			if (depth > 0 && go.name != tree.name) {
				return;
			}

			var componentsTypes = tree.components.Select (component => component.GetType ()).Distinct ();

			// remove components
			foreach (Component component in go.GetComponents<Component> ()) {
				if (component != null && !(component is Transform) && componentsTypes.Contains (component.GetType ())) {
					Object.DestroyImmediate (component);
				}
			}

			// children
			var children = GetChildren (go);
			foreach (Transform child in children) {
				TreeItem next = null;
				foreach (TreeItem treeChild in tree.children) {
					if (
						child.gameObject.name == treeChild.name &&
						child.gameObject.GetType ().ToString () == treeChild.type
					) {
						next = treeChild;
						RemoveWalkdown (child.gameObject, ref next, depth + 1);
						break;
					}
				}
			}
		}

		static Transform[] GetChildren (GameObject go) {
			int count = go.transform.childCount;
			var children = new Transform[count];

			for (int i = 0; i < count; ++i) {
				children[i] = go.transform.GetChild (i);
			}

			return children;
		}

		static void CopyProperties (Component srcComponent, Component dstComponent) {
			var dst = new SerializedObject (dstComponent);
			var src = new SerializedObject (srcComponent);

			dst.Update ();
			src.Update ();

			var iter = src.GetIterator ();
			while (iter.NextVisible (true)) {
				dst.CopyFromSerializedProperty (iter);
			}
			dst.ApplyModifiedProperties ();
		}

		static void UpdateProperties (Transform dstRoot) {
			foreach (Component dstComponent in components) {
				if (dstComponent == null) {
					continue;
				}

				// SkinnedMeshRenderer.bonesがなぜか下のSerializedObject経由での処理で更新されないので直接参照を更新する。
				if (dstComponent is SkinnedMeshRenderer) {
					SkinnedMeshRenderer dstRenderer = dstComponent as SkinnedMeshRenderer;
					var bones = dstRenderer.bones;
					for(int i = 0; i < bones.Length; i++) {
						Transform srcTransform = bones[i];

						Transform dstTransform = SearchDstTransform(dstRoot, srcTransform);
						if (dstTransform is null) continue;

						bones[i] = dstTransform;
					}

					dstRenderer.bones = bones;
				}

				var so = new SerializedObject (dstComponent);
				so.Update ();
				var iter = so.GetIterator ();

				// Object Reference
				while (iter.NextVisible (true)) {
					if (iter.propertyType.ToString () != "ObjectReference") {
						continue;
					}

					SerializedProperty property = so.FindProperty (iter.propertyPath);
					var dstObjectReference = property.objectReferenceValue;
					if (dstObjectReference == null) {
						continue;
					}
					if (!(dstObjectReference is Transform || dstObjectReference is Component)) {
						continue;
					}

					Transform srcTransform = null;
					if (dstObjectReference is Component) {
						srcTransform = (dstObjectReference as Component).transform;
					} else if (dstObjectReference is Transform) {
						srcTransform = dstObjectReference as Transform;
					}
					
					Transform dstTransform = SearchDstTransform(dstRoot, srcTransform);
					if (dstTransform is null) continue;

					if (dstObjectReference is Transform) {
						property.objectReferenceValue = dstTransform;
					} else if (dstObjectReference is Component) {
						Component comp = (Component)dstObjectReference;
						var children = dstTransform.GetComponents(dstObjectReference.GetType());
						var index = GetReferenceIndex(ref srcTransform, ref comp);

						if (!SearchObjectReference(ref copyTree, ref comp)) {
							continue;
						}
						if (index < 0) {
							continue;
						}

						property.objectReferenceValue = children[index];
					}
				}
				so.ApplyModifiedProperties ();
			}
		}

		static private Transform SearchDstTransform(Transform dstRoot, Transform srcTransform)
		{
			// ObjectReferenceの参照先がコピー内に存在するか
			if (!transforms.Contains(srcTransform)) {
				return null;
			}

			// コピー元のルートからObjectReferenceの位置への経路を探り、コピー後のツリーから該当オブジェクトを探す
			var routes = SearchRoute(root, srcTransform);
			if (routes == null) {
				return null;
			}

			Transform current = dstRoot;
			foreach (var route in routes) {
				// 次の子を探す(TreeItemの名前と型で経路と同じ子を探す)
				var children = GetChildren(current.gameObject);
				if (children.Length < 1) {
					current = null;
					break;
				}

				Transform next = null;
				foreach (Transform child in children) {
					var treeitem = new TreeItem(child.gameObject);
					if (treeitem.name == route.name && treeitem.type == route.type) {
						next = child;
						break;
					}
				}

				if (next == null) {
					current = null;
					break;
				}

				current = next;
			}

			return current;
		}

		static private int GetReferenceIndex(ref Transform current, ref Component component) {
			var children = current.GetComponents(component.GetType());
			int i = children.Length;

			while (--i >= 0) {
				if (children[i] == component) {
					break;
				}
			}

			return i;
		}

		static private bool SearchObjectReference(ref TreeItem treeitem, ref Component component) {
			if (treeitem.components.Contains(component)) {
				return true;
			}
			for(int i = 0, il = treeitem.children.Count(); i < il; ++i) {
				var child = treeitem.children[i];
				if (SearchObjectReference(ref child, ref component)) {
					return true;
				}
			}

			return false;
		}

		private void OnGUI () {
			activeObject = Selection.activeGameObject;
			EditorGUILayout.LabelField ("アクティブなオブジェクト");
			using (new GUILayout.VerticalScope (GUI.skin.box)) {
				EditorGUILayout.LabelField (activeObject ? activeObject.name : "");
			}
			if (!activeObject) {
				return;
			}

			pattern = EditorGUILayout.TextField ("正規表現", pattern);
			EditorUserSettings.SetConfigValue ("CopyComponentsByRegex/pattern", pattern);

			if (GUILayout.Button ("Copy")) {
				// initialize class variables
				copyTree = new TreeItem (activeObject);
				root = activeObject.transform;
				transforms = new List<Transform> ();
				components = new List<Component> ();

				var regex = new Regex (pattern);
				CopyWalkdown (activeObject, ref copyTree, ref regex);
			}

			EditorGUILayout.LabelField ("コピー中のオブジェクト");
			using (new GUILayout.VerticalScope (GUI.skin.box)) {
				EditorGUILayout.LabelField (root ? root.name : "");
			}

			EditorUserSettings.SetConfigValue (
				"CopyComponentsByRegex/copyTransform",
				(copyTransform = GUILayout.Toggle (copyTransform, "Transformがマッチした場合値をコピー")).ToString ()
			);
			EditorUserSettings.SetConfigValue (
				"CopyComponentsByRegex/isRemoveBeforeCopy",
				(isRemoveBeforeCopy = GUILayout.Toggle (isRemoveBeforeCopy, "コピー先に同じコンポーネントがあったら削除")).ToString ()
			);
			EditorUserSettings.SetConfigValue (
				"CopyComponentsByRegex/pasteValuesIfExists",
				(pasteValuesIfExists = GUILayout.Toggle (pasteValuesIfExists, "コピー先に同じコンポーネントがあったら値をペースト(複数ある場合は並び順依存)")).ToString ()
			);
			EditorUserSettings.SetConfigValue (
				"CopyComponentsByRegex/isObjectCopy",
				(isObjectCopy = GUILayout.Toggle (isObjectCopy, "コピー先にオブジェクトがなかったらオブジェクトをコピー")).ToString ()
			);
			EditorUserSettings.SetConfigValue (
				"CopyComponentsByRegex/isObjectCopySaveTransform",
				(isObjectCopySaveTransform = GUILayout.Toggle (isObjectCopySaveTransform, "オブジェクトのコピー時にコピー元のルートからの相対位置を保持")).ToString ()
			);
			EditorUserSettings.SetConfigValue (
				"CopyComponentsByRegex/isClothNNS",
				(isClothNNS = GUILayout.Toggle (isClothNNS, "ClothコンポーネントのConstraintsを一番近い頂点からコピー")).ToString ()
			);

			if (GUILayout.Button ("Paste")) {
				if (copyTree == null || root == null) {
					return;
				}

				if (isRemoveBeforeCopy) {
					RemoveWalkdown (activeObject, ref copyTree);
				}

				MergeWalkdown (activeObject, ref copyTree);
				UpdateProperties (activeObject.transform);
			}

			GUIStyle labelStyle = new GUIStyle (GUI.skin.label);
			labelStyle.wordWrap = true;
			using (new GUILayout.VerticalScope (GUI.skin.box)) {
				GUILayout.Label (
					"「一番近い頂点からコピー」を利用する場合はあらかじめClothのコピー先にClothを追加するか、" +
					"最初はチェックなしでコピーした後、別途Clothのみを対象にして「一番近い頂点からコピー」を行ってください。" +
					"\n(UnityのClothコンポーネントの初期化時に頂点座標がずれてるのが原因のため現在は修正困難です)",
					labelStyle
				);
			}
		}
	}
}