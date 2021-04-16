using System.Collections.Generic;
using Autodesk.Revit.DB;
using System;
using System.Linq;
using JPMorrow.Tools.Diagnostics;
using JPMorrow.Revit.Documents;

namespace JPMorrow.Tools.Revit
{

	public class MepSystemSearchCustom
	{
		private List<ElementId> visited = new List<ElementId>();
		private List<EndPointData> debugTagData = new List<EndPointData>();

		public List<JBox> GetJboxConnections(Document doc, List<ElementId> jboxes)
		{
			List<JBox> retBoxes = new List<JBox>();
			foreach(ElementId id in jboxes)
			{
				Element jBox = doc.GetElement(id);
				ConnectorSet cSet = GetConnectors(jBox);
				int connCnt = 0;
				List<Element> connectedCons = new List<Element>();
				foreach(Connector c in cSet)
				{
					bool connected = false;
					foreach(Connector cc in c.AllRefs)
					{
						if (cc.IsConnectedTo(c)) connected = true;
					}
					if (connected)
					{
						List<ElementId> jid = new List<ElementId>() { jBox.Id };
						Connector cCheck = c;
						connectedCons.Add(GetNextConnectedElement(ref cCheck,jBox,ref jid));
						connCnt++;
					}
				}

				if (connCnt == 1)
					retBoxes.Add(new OneConnJbox(jBox, connectedCons.ToArray()));
				else if (connCnt == 2)
					retBoxes.Add(new TwoConnJbox(jBox, connectedCons.ToArray()));
				else if (connCnt == 3)
					retBoxes.Add(new ThreeConnJbox(jBox, connectedCons.ToArray()));
				else if (connCnt == 4)
					retBoxes.Add(new FourConnJbox(jBox, connectedCons.ToArray()));
			}
			return retBoxes;
		}

		/// <summary>
		/// Get the elements of a run for processing (starts with conduit)
		/// </summary>
		/// <param name="firstConduit"></param>
		/// <returns></returns>
		public List<ElementId> GetRunNetworkConduit(Element firstConduit)
		{
			visited.Clear();
			Element currConduit = firstConduit;
			try
			{
				ConnectorSet pConnset = GetConnectors(currConduit);

				Connector conn1 = null;
				Connector conn2 = null;
				SplitConnectors(ref conn1, ref conn2, ref pConnset);
				if (conn1 != null) BuildNetworkFromOneEndConduit(conn1);
				if (conn2 != null) BuildNetworkFromOneEndConduit(conn2);
				else visited.Add(currConduit.Id);
			}
			catch
			{
				debugger.show(err:"Run Failed", max_itr:1);
			}


			//EndPointData.ResetCounter();
			visited = visited.GroupBy(x => x.IntegerValue).Select(x => x.First()).ToList();
			return visited;
		}

		private void BuildNetworkFromOneEndConduit(Connector c)
		{
			visited.Add(c.Owner.Id);
			foreach (Connector conn in c.AllRefs)
			{
				if (visited.Contains(conn.Owner.Id)) { continue; }
				ConnectorSet pConnset = GetConnectors(conn.Owner);
				foreach(Connector cc in pConnset)
				{
					try
					{
						if (cc == null) { debugger.show(err: "cc == null"); return; }
						if (!(cc.Owner.Category.Name == "Conduits" || cc.Owner.Category.Name == "Conduit Fittings")) { return; }
						if (cc.Origin.DistanceTo(c.Origin) > 0)
						{
							BuildNetworkFromOneEndConduit(cc);
						}
					}
					catch
					{
						debugger.show(err: "fail");
					}
				}
			}
		}

		/// <summary>
		/// Sets the parameters for a set of conduit
		/// </summary>
		/// <param name="conduits"></param>
		/// <param name="doc"></param>
		/// <param name="ps">From, To, Wire Size, Comments</param>
		public void SetConduitParameters(Document doc, List<ElementId> conduits, string[] ps)
		{
			foreach (ElementId conid in conduits)
			{
				Element conduit = doc.GetElement(conid);
				if (!ps.Any()) return;
				if (ps[0] != null)
					conduit.LookupParameter("From").Set(ps[0]);
				if (ps[1] != null)
					conduit.LookupParameter("To").Set(ps[1]);
				if (ps[2] != null)
					conduit.LookupParameter("Wire Size").Set(ps[2]);
				if (ps[3] != null)
					conduit.LookupParameter("Comments").Set(ps[3]);
			}
		}

		/// <summary>
		/// Get the elements of a run for processing (mixed conduit/jboxes)
		/// </summary>
		/// <param name="firstConduit"></param>
		/// <returns></returns>
		public List<ElementId> GetRunNetworkMix(Element firstConduit)
		{
			return new List<ElementId>();
		}

		private void SplitConnectors(ref Connector conn1, ref Connector conn2,
		ref ConnectorSet connSet, int conCount = 1)
		{
			foreach (Connector conn in connSet)
			{
				if (conn.IsConnected)
				{
					if (conCount == 1) { conn1 = conn; conCount++; }
					if (conCount == 2) { conn2 = conn; }
				}
			}
		}

		/// Gets the Element the Connector is connected
		/// to if it has not been visited before. Currently
		/// set to work with Pipe and FamilyInstance types
		/// - change types to accomodate
		/// </summary>
		/// <param name="pConn">The Connector from which we want to grab the connected Element</param>
		/// <param name="pPrevElem">The Element from which we are coming from</param>
		/// <param name="lVistied">List of visited Elements (by their Id's)</param>
		/// <returns></returns>
		public Element GetNextConnectedElement(
			ref Connector pConn,
			Element pPrevElem,
			ref List<ElementId> lVisited )
		{
			foreach (Connector pRef in pConn.AllRefs)
			{
				if (NextConnector(pRef, pPrevElem, lVisited))
				{
					continue;
				}
				//ConnectorSet nextConnSet = GetConnectors(pRef.Owner);
				//foreach (Connector c in nextConnSet)
				//{
				//	if () {  }
				//	if (!NextConnector(c, pRef.Owner, lVisited)) { pConn = c; }
				//}
				return pRef.Owner;
			}
			return null;
		}

		/// Return true if we want to look at the
		/// next Connector, and false if the current
		/// Connector is desired
		/// </summary>
		/// <param name="pConn">The Current Connector</param>
		/// <param name="pPrevElement">The previous Element the Connector came from</param>
		/// <param name="lVisited">A List of visited Elements (by their Id's)</param>
		/// <returns></returns>
		public bool NextConnector(
			Connector pConn,
			Element pPrevElement,
			List<ElementId> lVisited)
		{
			if(pConn.Owner.Category.Name == "Conduits") { return false; }
			if(pConn.Owner.Category.Name == "Conduit Fittings") { return false; }
			if (lVisited.Contains(pConn.Owner.Id)) { return true; }
			return true;
		}

		/// <summary>
		/// Return the given element's connector set.
		/// </summary>
		private static ConnectorSet GetConnectors(Element e)
		{
			if (e is MEPCurve)
				return ((MEPCurve)e)?
				  .ConnectorManager?
					.Connectors;

			if (e is FamilyInstance)
				return ((FamilyInstance)e)?
				  .MEPModel?
					.ConnectorManager?
					  .Connectors;

			return null;
		}
	}

	public class EndPointData
	{
		static int instanceCounter = 0;
		private int counter = 0;

		public double conDistance { get; set; }
		public XYZ location { get; set; }
		public bool wasPicked { get; set; }

		public EndPointData(double connd, XYZ loc)
		{
			instanceCounter++;
			conDistance = connd;
			location = loc;
			counter = instanceCounter;
			wasPicked = false;
		}

		public int GetCounter()
		{
			return counter;
		}

		public static void ResetCounter()
		{
			instanceCounter = 0;
		}
	}

	public class JBox
	{
		public Element[] ConnectedConduit { get; set; } = null;
		public Element Box { get; set; }
		public bool Invalid { get { return ConnectedConduit == null ? true : false; } }

		public bool JboxOrderCheck(ref List<ElementId> excludeIds)
		{
			foreach(Element el in ConnectedConduit)
			{
				if (excludeIds.Any(x => x.IntegerValue == el.Id.IntegerValue))
					return true;
			}
			return false;
		}
	}

	public class OneConnJbox : JBox
	{
		public OneConnJbox(Element box, Element[] conncon)
		{
			this.Box = box;
			if (conncon.Length == 1)
			{
				this.ConnectedConduit = new Element[1];
				conncon.CopyTo(this.ConnectedConduit, 0);
			}
			else
			{
				debugger.show(err: "Connection mismatch");
				this.ConnectedConduit = null;
			}
		}
	}

	public class TwoConnJbox : JBox
	{
		public TwoConnJbox(Element box, Element[] conncon)
		{
			this.Box = box;
			if (conncon.Length == 2)
			{
				this.ConnectedConduit = new Element[2];
				conncon.CopyTo(this.ConnectedConduit, 0);
			}
			else
			{
				debugger.show(err: "Connection mismatch");
				this.ConnectedConduit = null;
			}
		}
	}


	public class ThreeConnJbox : JBox
	{
		public ThreeConnJbox(Element box, Element[] conncon)
		{
			this.Box = box;
			if (conncon.Length == 3)
			{
				this.ConnectedConduit = new Element[3];
				conncon.CopyTo(this.ConnectedConduit, 0);
			}
			else
			{

				debugger.show(err: "Connection mismatch");
				this.ConnectedConduit = null;
			}
		}
	}

	public class FourConnJbox : JBox
	{
		public FourConnJbox(Element box, Element[] conncon)
		{
			this.Box = box;
			if (conncon.Length == 4)
			{
				this.ConnectedConduit = new Element[4];
				conncon.CopyTo(this.ConnectedConduit, 0);
			}
			else
			{
				debugger.show(err: "Connection mismatch");
				this.ConnectedConduit = null;
			}
		}
	}
}