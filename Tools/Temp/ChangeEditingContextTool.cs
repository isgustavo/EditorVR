﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.InputNew;

namespace UnityEditor.Experimental.EditorVR.Tools
{
	[MainMenuItem("Change Context", "Create", "Push and pop editing contexts")]
	//[MainMenuItem(false)]
	sealed class ChangeEditingContextTool : MonoBehaviour, ITool, ICustomActionMap, ISetEditingContext, ITooltip, 
		ISetTooltipVisibility, ITooltipPlacement, IUsesRayOrigin
	{
		[SerializeField]
		ActionMap m_ActionMap;

		int m_CurrentContextIndex;
		Transform m_TooltipTransform;

		public List<IEditingContext> allContexts { private get; set; }

		public ActionMap actionMap { get { return m_ActionMap; } }

		public string tooltipText { get; private set; }
		public Transform tooltipTarget { get { return m_TooltipTransform; } }
		public Transform tooltipSource { get { return rayOrigin; } }
		public TextAlignment tooltipAlignment { get { return TextAlignment.Center; } }

		public Transform rayOrigin { get; set; }

		void Awake()
		{
			m_TooltipTransform = ObjectUtils.CreateEmptyGameObject(null, transform).transform;
		}

		void OnDestroy()
		{
			if (gameObject.activeInHierarchy)
				this.HideTooltip(this);

			ObjectUtils.Destroy(m_TooltipTransform.gameObject);
		}

		public void ProcessInput(ActionMapInput input, ConsumeControlDelegate consumeControl)
		{
			var changeContextInput = (ChangeEditingContextInput)input;

			m_TooltipTransform.position = rayOrigin.position + Vector3.up * 0.1f;
			var rotation = MathUtilsExt.ConstrainYawRotation(rayOrigin.rotation);
			m_TooltipTransform.rotation = rotation;

			var change = changeContextInput.change;
			if (change.negative.wasJustPressed)
				m_CurrentContextIndex = Mathf.Max(m_CurrentContextIndex - 1, 0);
			else if (change.positive.wasJustReleased)
				m_CurrentContextIndex = Mathf.Min(m_CurrentContextIndex + 1, allContexts.Count - 1);
			consumeControl(change);

			tooltipText = allContexts[m_CurrentContextIndex].name;
			this.ShowTooltip(this);
			
			var push = changeContextInput.push;
			var pop = changeContextInput.pop;
			var set = changeContextInput.set;
			if (push.wasJustPressed)
			{
				consumeControl(push);
				EditorApplication.delayCall += () => this.PushEditingContext(allContexts[m_CurrentContextIndex]);
			}
			else if (pop.wasJustPressed)
			{
				consumeControl(pop);
				EditorApplication.delayCall += () => this.PopEditingContext();
			}
			else if (set.wasJustPressed)
			{
				consumeControl(set);
				EditorApplication.delayCall += () => this.SetEditingContext(allContexts[m_CurrentContextIndex]);
			}
		}
	}
}
#endif