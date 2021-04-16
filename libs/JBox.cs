using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using JPMorrow.Revit.Documents;
using JPMorrow.Revit.Tools;
using JPMorrow.Tools.Revit;

namespace JPMorrow.JBox
{
    public class JBox
	{
		public int ConnectionCount { get; private set; }
		public ElementId BoxId { get; private set; }
		public ElementId[] StartConduitIds { get; private set; }
		public ElementId[] ConduitIds { get; private set; }
		public string From { get; private set; }
		public string To { get; private set; }
		public string WireSize { get; private set; }
		public string Comments { get; private set; }
		public bool IsRan { get; set; } = false;

		private JBox(ModelInfo info, ElementId jbox)
		{
			var jb_el = info.DOC.GetElement(jbox);
			var jb = RJBI.ParseJunctionBox(jbox, info);
			ConnectionCount = jb.connected_conduit_ids.Count();
			StartConduitIds = jb.connected_conduit_ids.Select(x => new ElementId(x)).ToArray();

			MepSystemSearchCustom mssc = new MepSystemSearchCustom();
			List<ElementId> ids = new List<ElementId>();
			foreach(var c in jb.connected_conduit_ids)
			{
				var potential_ids = mssc.GetRunNetworkConduit(info.DOC.GetElement(new ElementId(c)));
				ids.AddRange(potential_ids);
			}

			ConduitIds = ids.ToArray();
			BoxId = new ElementId(jb.jbox);

			From = jb_el.LookupParameter("From").AsString();
			To = jb_el.LookupParameter("To").AsString();
			WireSize = jb_el.LookupParameter("Wire Size").AsString();
			Comments = jb_el.LookupParameter("Comments").AsString();
		}

		public static IEnumerable<JBox> ProcessIdsToBoxes(ModelInfo info, IEnumerable<ElementId> jbox_ids)
		{
			var jboxes = new List<JBox>();
			jbox_ids.ToList().ForEach(x => jboxes.Add(new JBox(info, x)));
			return jboxes;
		}

		/// <summary>
		/// This function checks to see if there are any other jboxes that share the pipe of this one
		/// </summary>
		public static bool CheckPipeCollision(ElementId start_con, IEnumerable<JBox> sortjb)
		{
			foreach(var jb in sortjb.Where(x => x.IsRan))
			{
				if(jb.ConduitIds.Any(x => x.IntegerValue == start_con.IntegerValue) || jb.StartConduitIds.Any(x => x.IntegerValue == start_con.IntegerValue)) return true;
			}
			return false;
		}

		public void PropogateJboxInfo(ModelInfo info, ElementId start_con)
		{
			MepSystemSearchCustom mssc = new MepSystemSearchCustom();
			var ids = mssc.GetRunNetworkConduit(info.DOC.GetElement(start_con));
			ids.Add(start_con);

			foreach(var id in ids)
			{
				var el = info.DOC.GetElement(id);
				el.LookupParameter("From").Set(From);
				el.LookupParameter("To").Set(To);
				el.LookupParameter("Wire Size").Set(WireSize);
				el.LookupParameter("Comments").Set(Comments);
			}
		}
	}
}