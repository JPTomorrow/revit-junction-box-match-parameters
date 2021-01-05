/*
 * Created by SharpDevelop.
 * User: jmorrow
 * Date: 9/17/2018
 * Time: 6:28 AM
 *
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using JPMorrow.Tools.Diagnostics;
using JPMorrow.Revit.Documents;
using JBox = JPMorrow.JBox.JBox;

namespace MainApp
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("58F7B2B7-BF6D-4B39-BBF8-13F7D9AAE97E")]
	public partial class ThisApplication : IExternalCommand
	{
		public Result Execute(ExternalCommandData cData, ref string message, ElementSet elements)
        {
			var dataDirectories = new string[0];
			var debugApp = false;

			//set revit documents
			ModelInfo info = ModelInfo.StoreDocuments(cData, dataDirectories, debugApp);

			List<ElementId> jboxes = info.UIDOC.Selection.GetElementIds().ToList();

			List<ElementId> boxesFiltered = new List<ElementId>();
			foreach (ElementId id in jboxes)
			{
				Element box = info.DOC.GetElement(id);
				if (box.Category.Name != "Electrical Fixtures") continue;
				if (String.IsNullOrWhiteSpace(box.LookupParameter("From").AsString()) ||
					String.IsNullOrWhiteSpace(box.LookupParameter("To").AsString())) continue;
				boxesFiltered.Add(id);
			}

			//fail if no boxes selected
			if (!boxesFiltered.Any())
			{
				debugger.show(
					header:	"Junction Boxes",
					sub:	"None Selected",
					err:	"No junction boxes were selected. " +
							"please select them before running the script.");
				return Result.Succeeded;
			}

			//check for parameters and quit if null
			Element testBox = info.DOC.GetElement(boxesFiltered.First());
			if (testBox.LookupParameter("From") == null &&
				testBox.LookupParameter("To") == null &&
				testBox.LookupParameter("Wire Size") == null &&
				testBox.LookupParameter("Comments") == null)
			{
				debugger.show(
					header: "Junction Boxes",
					sub: "Parameters",
					err: "You do not have the 'To', 'From', or 'Wire Size' parameters " +
							"loaded for electrical fixtures. These parameters " +
							"are required in order for this program to run.");
				return Result.Succeeded;
			}
			int successCnt = 0;
			using (TransactionGroup tgx = new TransactionGroup(info.DOC, "Propogating parameters"))
			{
				tgx.Start();

				// get boxes
				var pboxes = JBox.ProcessIdsToBoxes(info, boxesFiltered).ToList();
				pboxes.OrderBy(x => x.ConnectionCount).ToList();
				Queue<JBox> final_boxes = new Queue<JBox>(pboxes);
				List<JBox> ran_boxes = new List<JBox>();
				string o = "";
				int try_cnt = 0;

				while(final_boxes.Any())
				{
					var box = final_boxes.Dequeue();

					int conns = 0;
					List<bool> start_pattern = new List<bool>();
					foreach(var c in box.StartConduitIds)
						if(JBox.CheckPipeCollision(c, ran_boxes))
						{
							conns++;
							start_pattern.Add(true);
						}
						else
							start_pattern.Add(false);


					if(conns <= 0 && box.ConnectionCount > 1 && final_boxes.Count != 0 && try_cnt <= final_boxes.Count())
					{
						try_cnt++;
						o += "pass: " + conns.ToString() + " | " + box.ConnectionCount.ToString() + "\n";
						//debugger.show(err:o);
						final_boxes.Enqueue(box);
						continue;
					}
					else
					{
						try_cnt = 0;
						o += "continue: " + conns.ToString() + " | " + box.ConnectionCount.ToString() + "\n";
						//debugger.show(err:o);
					}

					box.IsRan = true;
					ran_boxes.Add(box);

					for(var i = 0; i < box.StartConduitIds.Count(); i++)
					{
						using (Transaction tx = new Transaction(info.DOC, "parameters"))
						{
							tx.Start();
							if(start_pattern[i]) continue;
							box.PropogateJboxInfo(info, box.StartConduitIds[i]);
							tx.Commit();
						}
					}

					successCnt++;
				}
				tgx.Assimilate();
			}

			debugger.show(	header:"JBox > Parameters",
													sub:"Results",
													err:successCnt.ToString() + " junction boxes had their parameters " +
														"pushed to thier associated conduit runs.");
			return Result.Succeeded;
        }
	}
}