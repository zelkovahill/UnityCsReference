// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor.AnimatedValues;

namespace UnityEditor
{
    [CanEditMultipleObjects]
    internal abstract class Collider2DEditorBase : Editor
    {
        protected class Styles
        {
            public static readonly GUIContent s_ColliderEditDisableHelp = EditorGUIUtility.TrTextContent("Collider cannot be edited because it is driven by SpriteRenderer's tiling properties.");
            public static readonly GUIContent s_AutoTilingLabel = EditorGUIUtility.TrTextContent("Auto Tiling ", " When enabled, the collider's shape will update automatically based on the SpriteRenderer's tiling properties");
        }

        private SerializedProperty m_Density;
        private readonly AnimBool m_ShowDensity = new AnimBool();
        private readonly AnimBool m_ShowLayerOverrides = new AnimBool();
        private readonly AnimBool m_ShowInfo = new AnimBool();
        private readonly AnimBool m_ShowContacts = new AnimBool();
        Vector2 m_ContactScrollPosition;

        static ContactPoint2D[] m_Contacts = new ContactPoint2D[100];

        private SavedBool m_ShowLayerOverridesFoldout;
        private SavedBool m_ShowInfoFoldout;
        private bool m_RequiresConstantRepaint;

        private SerializedProperty m_Material;
        private SerializedProperty m_IsTrigger;
        private SerializedProperty m_UsedByEffector;
        private SerializedProperty m_UsedByComposite;
        private SerializedProperty m_Offset;
        protected SerializedProperty m_AutoTiling;
        private SerializedProperty m_LayerOverridePriority;
        private SerializedProperty m_IncludeLayers;
        private SerializedProperty m_ExcludeLayers;
        private SerializedProperty m_ForceSendLayers;
        private SerializedProperty m_ForceReceiveLayers;
        private SerializedProperty m_ContactCaptureLayers;
        private SerializedProperty m_CallbackLayers;

        private readonly AnimBool m_ShowCompositeRedundants = new AnimBool();

        public virtual void OnEnable()
        {
            EditMode.editModeStarted += OnEditModeStart;
            EditMode.editModeEnded += OnEditModeEnd;

            m_Density = serializedObject.FindProperty("m_Density");

            m_ShowDensity.value = ShouldShowDensity();
            m_ShowDensity.valueChanged.AddListener(Repaint);

            m_ShowLayerOverrides.valueChanged.AddListener(Repaint);
            m_ShowLayerOverridesFoldout = new SavedBool($"{target.GetType() }.ShowLayerOverridesFoldout", false);
            m_ShowLayerOverrides.value = m_ShowLayerOverridesFoldout.value;

            m_ShowInfo.valueChanged.AddListener(Repaint);
            m_ShowInfoFoldout = new SavedBool($"{target.GetType()}.ShowInfoFoldout", false);
            m_ShowInfo.value = m_ShowInfoFoldout.value;
            m_ShowContacts.valueChanged.AddListener(Repaint);
            m_ContactScrollPosition = Vector2.zero;

            m_Material = serializedObject.FindProperty("m_Material");
            m_IsTrigger = serializedObject.FindProperty("m_IsTrigger");
            m_UsedByEffector = serializedObject.FindProperty("m_UsedByEffector");
            m_UsedByComposite = serializedObject.FindProperty("m_UsedByComposite");
            m_Offset = serializedObject.FindProperty("m_Offset");
            m_AutoTiling = serializedObject.FindProperty("m_AutoTiling");
            m_LayerOverridePriority = serializedObject.FindProperty("m_LayerOverridePriority");
            m_IncludeLayers = serializedObject.FindProperty("m_IncludeLayers");
            m_ExcludeLayers = serializedObject.FindProperty("m_ExcludeLayers");
            m_ForceSendLayers = serializedObject.FindProperty("m_ForceSendLayers");
            m_ForceReceiveLayers = serializedObject.FindProperty("m_ForceReceiveLayers");
            m_ContactCaptureLayers = serializedObject.FindProperty("m_ContactCaptureLayers");
            m_CallbackLayers = serializedObject.FindProperty("m_CallbackLayers");

            m_ShowCompositeRedundants.value = !m_UsedByComposite.boolValue;
            m_ShowCompositeRedundants.valueChanged.AddListener(Repaint);

            m_RequiresConstantRepaint = false;
        }

        public virtual void OnDisable()
        {
            m_ShowDensity.valueChanged.RemoveListener(Repaint);
            m_ShowLayerOverrides.valueChanged.RemoveListener(Repaint);
            m_ShowInfo.valueChanged.RemoveListener(Repaint);
            m_ShowContacts.valueChanged.RemoveListener(Repaint);
            m_ShowCompositeRedundants.valueChanged.RemoveListener(Repaint);

            EditMode.editModeStarted -= OnEditModeStart;
            EditMode.editModeEnded -= OnEditModeEnd;
        }

        public override void OnInspectorGUI()
        {
            m_ShowCompositeRedundants.target = !m_UsedByComposite.boolValue;
            if (EditorGUILayout.BeginFadeGroup(m_ShowCompositeRedundants.faded))
            {
                // Density property.
                m_ShowDensity.target = ShouldShowDensity();
                if (EditorGUILayout.BeginFadeGroup(m_ShowDensity.faded))
                    EditorGUILayout.PropertyField(m_Density);
                EditorGUILayout.EndFadeGroup();

                EditorGUILayout.PropertyField(m_Material);
                EditorGUILayout.PropertyField(m_IsTrigger);
                EditorGUILayout.PropertyField(m_UsedByEffector);
            }
            EditorGUILayout.EndFadeGroup();

            // Only show 'Used By Composite' if all targets are capable of being composited.
            if (targets.Count(x => (x as Collider2D).compositeCapable == false) == 0)
                EditorGUILayout.PropertyField(m_UsedByComposite);

            if (m_AutoTiling != null)
                EditorGUILayout.PropertyField(m_AutoTiling, Styles.s_AutoTilingLabel);

            EditorGUILayout.PropertyField(m_Offset);
        }

        public void FinalizeInspectorGUI()
        {
            ShowLayerOverridesProperties();
            ShowColliderInfoProperties();

            // Check for collider error state.
            CheckColliderErrorState();

            // If used-by-composite is enabled but there is not composite then show a warning.
            if (targets.Length == 1)
            {
                var collider = target as Collider2D;
                if (collider.isActiveAndEnabled && collider.composite == null && m_UsedByComposite.boolValue)
                    EditorGUILayout.HelpBox("This collider will not function with a composite until there is a CompositeCollider2D on the GameObject that the attached Rigidbody2D is on.", MessageType.Warning);
            }

            // Check for effector warnings.
            Effector2DEditor.CheckEffectorWarnings(target as Collider2D);

            EndColliderInspector();
        }

        private void ShowLayerOverridesProperties()
        {
            // Show Layer Overrides.
            m_ShowLayerOverridesFoldout.value = m_ShowLayerOverrides.target = EditorGUILayout.Foldout(m_ShowLayerOverrides.target, "Layer Overrides", true);
            if (EditorGUILayout.BeginFadeGroup(m_ShowLayerOverrides.faded))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_LayerOverridePriority);
                EditorGUILayout.PropertyField(m_IncludeLayers);
                EditorGUILayout.PropertyField(m_ExcludeLayers);

                // Only show force send/receive if we're not dealing with triggers.
                if (targets.Count(x => (x as Collider2D).isTrigger) == 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(m_ForceSendLayers);
                    EditorGUILayout.PropertyField(m_ForceReceiveLayers);
                }

                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(m_ContactCaptureLayers);
                EditorGUILayout.PropertyField(m_CallbackLayers);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();
        }

        private void ShowColliderInfoProperties()
        {
            m_RequiresConstantRepaint = false;

            m_ShowInfoFoldout.value = m_ShowInfo.target = EditorGUILayout.Foldout(m_ShowInfo.target, "Info", true);
            if (EditorGUILayout.BeginFadeGroup(m_ShowInfo.faded))
            {
                if (targets.Length == 1)
                {
                    var collider = targets[0] as Collider2D;
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("Attached Body", collider.attachedRigidbody, typeof(Rigidbody2D), false);
                    EditorGUILayout.FloatField("Friction", collider.friction);
                    EditorGUILayout.FloatField("Bounciness", collider.bounciness);
                    EditorGUILayout.FloatField("Shape Count", collider.shapeCount);
                    if (collider.isActiveAndEnabled)
                        EditorGUILayout.BoundsField("Bounds", collider.bounds);
                    EditorGUI.EndDisabledGroup();

                    ShowContacts(collider);

                    // We need to repaint as some of the above properties can change without causing a repaint.
                    m_RequiresConstantRepaint = true;
                }
                else
                {
                    EditorGUILayout.HelpBox("Cannot show Info properties when multiple colliders are selected.", MessageType.Info);
                }
            }
            EditorGUILayout.EndFadeGroup();
        }

        bool ShouldShowDensity()
        {
            if (targets.Select(x => (x as Collider2D).attachedRigidbody).Distinct().Count() > 1)
                return false;

            var rigidbody = (target as Collider2D).attachedRigidbody;
            return rigidbody && rigidbody.useAutoMass && rigidbody.bodyType == RigidbodyType2D.Dynamic;
        }

        void ShowContacts(Collider2D collider)
        {
            EditorGUI.indentLevel++;
            m_ShowContacts.target = EditorGUILayout.Foldout(m_ShowContacts.target, "Contacts", true);
            if (EditorGUILayout.BeginFadeGroup(m_ShowContacts.faded))
            {
                var contactCount = collider.GetContacts(m_Contacts);
                if (contactCount > 0)
                {
                    m_ContactScrollPosition = EditorGUILayout.BeginScrollView(m_ContactScrollPosition, GUILayout.Height(180));
                    EditorGUI.BeginDisabledGroup(true);

                    EditorGUILayout.IntField("Contact Count", contactCount);
                    EditorGUILayout.Space();

                    for (var i = 0; i < contactCount; ++i)
                    {
                        var contact = m_Contacts[i];
                        EditorGUILayout.HelpBox(string.Format("Contact#{0}", i), MessageType.None);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.Vector2Field("Point", contact.point);
                        EditorGUILayout.Vector2Field("Normal", contact.normal);
                        EditorGUILayout.Vector2Field("Relative Velocity", contact.relativeVelocity);
                        EditorGUILayout.FloatField("Normal Impulse", contact.normalImpulse);
                        EditorGUILayout.FloatField("Tangent Impulse", contact.tangentImpulse);
                        EditorGUILayout.ObjectField("Collider", contact.collider, typeof(Collider2D), false);
                        EditorGUILayout.ObjectField("Rigidbody", contact.rigidbody, typeof(Rigidbody2D), false);
                        EditorGUILayout.ObjectField("OtherRigidbody", contact.otherRigidbody, typeof(Rigidbody2D), false);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.Space();
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.HelpBox("No Contacts", MessageType.Info);
                }
            }
            EditorGUILayout.EndFadeGroup();
            EditorGUI.indentLevel--;
        }

        internal override void OnForceReloadInspector()
        {
            base.OnForceReloadInspector();

            // Whenever inspector get reloaded (reset, move up/down), quit the edit mode if was in editing mode.
            // Not sure why this pattern is used here but not for any other editors that implement edit mode button
            if (editingCollider)
                EditMode.QuitEditMode();
        }

        protected void CheckColliderErrorState()
        {
            switch ((target as Collider2D).errorState)
            {
                case ColliderErrorState2D.NoShapes:
                    // Show warning.
                    EditorGUILayout.HelpBox("The collider did not create any collision shapes as they all failed verification.  This could be because they were deemed too small or the vertices were too close.  Vertices can also become close under certain rotations or very small scaling.", MessageType.Warning);
                    break;

                case ColliderErrorState2D.RemovedShapes:
                    // Show warning.
                    EditorGUILayout.HelpBox("The collider created collision shape(s) but some were removed as they failed verification.  This could be because they were deemed too small or the vertices were too close.  Vertices can also become close under certain rotations or very small scaling.", MessageType.Warning);
                    break;
            }
        }

        protected void BeginEditColliderInspector()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(targets.Length > 1))
            {
                EditorGUILayout.EditorToolbarForTarget(EditorGUIUtility.TrTempContent("Edit Collider"), this);
            }
        }

        protected void EndColliderInspector()
        {
            serializedObject.ApplyModifiedProperties();
        }

        protected bool CanEditCollider()
        {
            var e = targets.FirstOrDefault((x) =>
            {
                var sr = (x as Component).GetComponent<SpriteRenderer>();
                return (sr != null && sr.drawMode != SpriteDrawMode.Simple && m_AutoTiling.boolValue == true);
            }
            );
            return e == false;
        }

        public override bool RequiresConstantRepaint()
        {
            return m_RequiresConstantRepaint;
        }

        protected virtual void OnEditStart() {}
        protected virtual void OnEditEnd() {}

        public bool editingCollider
        {
            get { return EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(this); }
        }

        protected virtual GUIContent editModeButton { get { return EditorGUIUtility.IconContent("EditCollider"); } }

        protected void InspectorEditButtonGUI()
        {
            EditMode.DoEditModeInspectorModeButton(
                EditMode.SceneViewEditMode.Collider,
                "Edit Collider",
                editModeButton,
                this
            );
        }

        internal override Bounds GetWorldBoundsOfTarget(Object targetObject)
        {
            if (targetObject is Collider2D)
                return ((Collider2D)targetObject).bounds;
            else if (targetObject is Collider)
                return ((Collider)targetObject).bounds;
            else
                return base.GetWorldBoundsOfTarget(targetObject);
        }

        protected void OnEditModeStart(IToolModeOwner owner, EditMode.SceneViewEditMode mode)
        {
            if (mode == EditMode.SceneViewEditMode.Collider && owner == (IToolModeOwner)this)
                OnEditStart();
        }

        protected void OnEditModeEnd(IToolModeOwner owner)
        {
            if (owner == (IToolModeOwner)this)
                OnEditEnd();
        }
    }
}