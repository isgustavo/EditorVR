﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputNew;

namespace UnityEngine.VR.Proxies
{
	public class MultipleRayInputModule : PointerInputModule
	{
		[SerializeField]
		public Camera EventCameraPrefab; // Camera to be instantiated and assigned to EventCamera property

		private readonly List<RaycastSource> m_RaycastSources = new List<RaycastSource>();
		private Dictionary<Transform, int> m_RayOriginToPointerID = new Dictionary<Transform, int>();
		private List<PointerEventData> PointEvents = new List<PointerEventData>();
		private List<GameObject> CurrentPoint = new List<GameObject>();
		private List<GameObject> CurrentPressed = new List<GameObject>();
		private List<GameObject> CurrentDragging = new List<GameObject>();

		public Camera eventCamera
		{
			get { return m_EventCamera; }
			set { m_EventCamera = value; }
		}
		private Camera m_EventCamera;

		public ActionMap actionMap
		{
			get { return m_UIActionMap; }
		}

		[SerializeField]
		private ActionMap m_UIActionMap;
		private int UILayer = -1;

		void Awake()
		{
			UILayer = LayerMask.NameToLayer("UI");
		}
		private class RaycastSource
		{
			public IProxy proxy; // Needed for checking if proxy is active
			public Node node;
			public Transform rayOrigin;
			public UIActions actionMapInput;
			public int TagIndex;

			public RaycastSource(IProxy proxy, Node node, Transform rayOrigin, UIActions actionMapInput)
			{
				this.proxy = proxy;
				this.node = node;
				this.rayOrigin = rayOrigin;
				this.actionMapInput = actionMapInput;
			}
		}

		public void AddRaycastSource(IProxy proxy, Node node, ActionMapInput actionMapInput)
		{
			UIActions actions = (UIActions) actionMapInput;
			if (actions == null)
			{
				Debug.LogError("Cannot add actionMapInput to InputModule that is not of type UIActions.");
				return;
			}
			actions.active = false;
			Transform rayOrigin = null;
			if (proxy.rayOrigins.TryGetValue(node, out rayOrigin))
			{
				m_RayOriginToPointerID.Add(rayOrigin, m_RaycastSources.Count);
				m_RaycastSources.Add(new RaycastSource(proxy, node, rayOrigin, actions));
			}
			else
				Debug.LogError("Failed to get ray origin transform for node " + node + " from proxy " + proxy);
		}

		public Transform GetRayOrigin(int index)
		{
			return m_RaycastSources[index].rayOrigin;
		}

		public PointerEventData GetPointerEventData(Transform rayOrigin)
		{
			int id;
			if (m_RayOriginToPointerID.TryGetValue(rayOrigin, out id))
			{
				if(id >= 0 && id < PointEvents.Count)
					return PointEvents[id];
			}

			return null;
		}

		public override void Process()
		{
			ExecuteUpdateOnSelectedObject();

			if (m_EventCamera == null)
				return;

			//Process events for all different transforms in RayOrigins
			for (int i = 0; i < m_RaycastSources.Count; i++)
			{
				// Expand lists if needed
				while (i >= CurrentPoint.Count)
					CurrentPoint.Add(null);
				while (i >= CurrentPressed.Count)
					CurrentPressed.Add(null);
				while (i >= CurrentDragging.Count)
					CurrentDragging.Add(null);
				while (i >= PointEvents.Count)
					PointEvents.Add(new PointerEventData(base.eventSystem));

				PointEvents[i].pointerId = i;

				if (!m_RaycastSources[i].proxy.active)
					continue;

				CurrentPoint[i] = GetRayIntersection(i); // Check all currently running raycasters

				HandlePointerExitAndEnter(PointEvents[i], CurrentPoint[i]); // Send enter and exit events

				// Activate actionmap input only if pointer is interacting with something
				m_RaycastSources[i].actionMapInput.active = (CurrentPoint[i] != null && CurrentPoint[i].layer == UILayer) || 
															CurrentPressed[i] != null ||
															CurrentDragging[i] != null;

				if (!m_RaycastSources[i].actionMapInput.active)
					continue;

				// Send select pressed and released events
				if (m_RaycastSources[i].actionMapInput.select.wasJustPressed)
					OnSelectPressed(i);

				if (m_RaycastSources[i].actionMapInput.select.wasJustReleased)
					OnSelectReleased(i);

				if (CurrentDragging[i] != null)
					ExecuteEvents.Execute(CurrentDragging[i], PointEvents[i], ExecuteEvents.dragHandler);

				// Send scroll events
				if (CurrentPressed[i] != null)
				{
					PointEvents[i].scrollDelta = new Vector2(0f, m_RaycastSources[i].actionMapInput.verticalScroll.value);
					ExecuteEvents.ExecuteHierarchy(CurrentPoint[i], PointEvents[i], ExecuteEvents.scrollHandler);
				}

				m_PointerData[i] = PointEvents[i];
			}

		}

		private void OnSelectPressed(int i)
		{
			Deselect();

			PointEvents[i].pressPosition = PointEvents[i].position;
			PointEvents[i].pointerPressRaycast = PointEvents[i].pointerCurrentRaycast;
			PointEvents[i].pointerPress = CurrentPoint[i];

			if (CurrentPoint[i] != null) // Pressed when pointer is over something
			{
				CurrentPressed[i] = CurrentPoint[i];
				GameObject newPressed = ExecuteEvents.ExecuteHierarchy(CurrentPressed[i], PointEvents[i], ExecuteEvents.pointerDownHandler);

				if (newPressed == null) // Gameobject does not have pointerDownHandler in hierarchy, but may still have click handler
					newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(CurrentPressed[i]);

				if (newPressed != null)
				{
					CurrentPressed[i] = newPressed; // Set current pressed to gameObject that handles the pointerDown event, not the root object
					PointEvents[i].pointerPress = newPressed;
					Select(CurrentPressed[i]);
					PointEvents[i].eligibleForClick = true;
				}
				ExecuteEvents.Execute(CurrentPressed[i], PointEvents[i], ExecuteEvents.beginDragHandler);
				PointEvents[i].pointerDrag = CurrentPressed[i];
				CurrentDragging[i] = CurrentPressed[i];
			}
		}

		private void OnSelectReleased(int i)
		{
			if (CurrentPressed[i])
				ExecuteEvents.Execute(CurrentPressed[i], PointEvents[i], ExecuteEvents.pointerUpHandler);

			if (CurrentDragging[i])
			{
				ExecuteEvents.Execute(CurrentDragging[i], PointEvents[i], ExecuteEvents.endDragHandler);
				if (CurrentPoint[i] != null)
					ExecuteEvents.ExecuteHierarchy(CurrentPoint[i], PointEvents[i], ExecuteEvents.dropHandler);

				PointEvents[i].pointerDrag = null;
				CurrentDragging[i] = null;
			}

			var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(CurrentPoint[i]);
			if (CurrentPressed[i] == clickHandler && PointEvents[i].eligibleForClick)
				ExecuteEvents.Execute(clickHandler, PointEvents[i], ExecuteEvents.pointerClickHandler);

			PointEvents[i].rawPointerPress = null;
			PointEvents[i].pointerPress = null;
			PointEvents[i].eligibleForClick = false;
			CurrentPressed[i] = null;
		}

		public void Deselect()
		{
			if (base.eventSystem.currentSelectedGameObject)
				base.eventSystem.SetSelectedGameObject(null);
		}

		private void Select(GameObject go)
		{
			Deselect();

			if (ExecuteEvents.GetEventHandler<ISelectHandler>(go))
				base.eventSystem.SetSelectedGameObject(go);
		}

		private GameObject GetRayIntersection(int i)
		{
			GameObject hit = null;
			// Move camera to position and rotation for the ray origin
			m_EventCamera.transform.position = m_RaycastSources[i].rayOrigin.position;
			m_EventCamera.transform.rotation = m_RaycastSources[i].rayOrigin.rotation;

			PointEvents[i].Reset();
			PointEvents[i].delta = Vector2.zero;
			PointEvents[i].position = m_EventCamera.pixelRect.center;
			PointEvents[i].scrollDelta = Vector2.zero;

			List<RaycastResult> results = new List<RaycastResult>();
			eventSystem.RaycastAll(PointEvents[i], results);
			PointEvents[i].pointerCurrentRaycast = FindFirstRaycast(results);
			hit = PointEvents[i].pointerCurrentRaycast.gameObject;

			m_RaycastResultCache.Clear();
			return hit;
		}

		private bool ExecuteUpdateOnSelectedObject()
		{
			if (base.eventSystem.currentSelectedGameObject == null)
				return false;

			BaseEventData eventData = GetBaseEventData();
			ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, eventData, ExecuteEvents.updateSelectedHandler);
			return eventData.used;
		}
	}
}