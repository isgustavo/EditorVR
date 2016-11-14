﻿using UnityEditor;
using UnityEngine.VR.Utilities;

namespace UnityEngine.VR.Actions
{
	[ActionMenuItem("Delete", ActionMenuItemAttribute.kDefaultActionSectionName, 7)]
	public class Delete : BaseAction
	{
		public override void ExecuteAction()
		{
			var selection = Selection.activeObject;
			if (selection)
			{
				U.Object.Destroy(selection);
				Selection.activeObject = null;
			}
		}
	}
}