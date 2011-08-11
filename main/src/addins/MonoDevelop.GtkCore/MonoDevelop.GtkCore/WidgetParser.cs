//
// ObjectsDocument.cs
//
// Authors:
//   Lluis Sanchez Gual
//   Mike Kestner
//
// Copyright (C) 2006-2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Xml;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using ICSharpCode.NRefactory.TypeSystem;

namespace MonoDevelop.GtkCore
{

	public class WidgetParser
	{

		ITypeResolveContext ctx;

		public WidgetParser (ITypeResolveContext ctx)
		{
			this.ctx = ctx;
		}
		
		public Dictionary<string, IType> GetToolboxItems ()
		{
			Dictionary<string, IType> tb_items = new Dictionary<string, IType> ();

			var wt = ctx.GetTypeDefinition ("Gtk", "Widget", 0, StringComparer.Ordinal);
			if (wt != null) {
				foreach (var t in wt.GetSubTypeDefinitions (ctx)) {
					if (t.ProjectContent == ctx && IsToolboxWidget (t))
						tb_items [t.FullName] = t;
				}
			}
			
			return tb_items;
		}

		public void CollectMembers (ITypeDefinition cls, bool inherited, string topType, ListDictionary properties, ListDictionary events)
		{
			if (cls.FullName == topType)
				return;

			foreach (IProperty prop in cls.Properties)
				if (IsBrowsable (prop))
					properties [prop.Name] = prop;

			foreach (IEvent ev in cls.Events)
				if (IsBrowsable (ev))
					events [ev.Name] = ev;
					
			if (inherited) {
				foreach (var bt in cls.BaseTypes) {
					var bcls = bt.Resolve (ctx).GetDefinition ();
					if (bcls != null && bcls.Kind != TypeKind.Class)
						CollectMembers (bcls, true, topType, properties, events);
				}
			}
		}
		
		public string GetBaseType (ITypeDefinition cls, Hashtable knownTypes)
		{
			foreach (var bt in cls.BaseTypes) {
				string name = bt.Resolve (ctx).ReflectionName;
				if (knownTypes.Contains (name))
					return name;
			}

			foreach (var bt in cls.BaseTypes) {
				var bcls = bt.Resolve (ctx).GetDefinition ();
				if (bcls != null) {
					string ret = GetBaseType (bcls, knownTypes);
					if (ret != null)
						return ret;
				}
			}
			return null;
		}
		
		public string GetCategory (IEntity decoration)
		{
//			foreach (IAttributeSection section in decoration.Attributes) {
				foreach (IAttribute at in decoration.Attributes) {
					var type = at.AttributeType.Resolve (ctx);
					switch (type.ReflectionName) {
					case "Category":
					case "CategoryAttribute":
					case "System.ComponentModel.Category":
					case "System.ComponentModel.CategoryAttribute":
						break;
					default:
						continue;
					}
					var pargs = at.GetPositionalArguments (ctx);
					if (pargs != null && pargs.Count > 0) {
						var val = pargs[0].GetValue (ctx);
						if (val is string)
							return val.ToString ();
					}
				}
	//	}
			return "";
		}
		
		public IType GetClass (string classname)
		{
			string name, ns;
			int idx =classname.LastIndexOf ('.');
			if (idx >= 0){
				ns = classname.Substring (0, idx);
				name = classname.Substring (idx + 1);
			} else {
				ns = "";
				name = classname;
			}
			return ctx.GetTypeDefinition (ns, name, 0, StringComparer.Ordinal);
		}

		public bool IsBrowsable (IMember member)
		{
			if (!member.IsPublic)
				return false;

			IProperty prop = member as IProperty;
			if (prop != null) {
				if (!prop.CanGet || !prop.CanSet)
					return false;
				if (Array.IndexOf (supported_types, prop.ReturnType.Resolve (ctx).ReflectionName) == -1)
					return false;
			}

	//		foreach (IAttributeSection section in member.Attributes) {
				foreach (IAttribute at in member.Attributes) {
					var type = at.AttributeType.Resolve (ctx);
					switch (type.ReflectionName) {
					case "Browsable":
					case "BrowsableAttribute":
					case "System.ComponentModel.Browsable":
					case "System.ComponentModel.BrowsableAttribute":
						break;
					default:
						continue;
					}
					var pargs = at.GetPositionalArguments (ctx);
					if (pargs != null && pargs.Count > 0) {
						var val = pargs[0].GetValue (ctx);
						if (val is bool) {
							return (bool) val;
						}
					}
				}
		//	}
			return true;
		}
		
		public bool IsToolboxWidget (ITypeDefinition cls)
		{
			if (!cls.IsPublic)
				return false;

			foreach (IAttribute at in cls.Attributes) {
				var type = at.AttributeType.Resolve (ctx);
				switch (type.ReflectionName) {
				case "ToolboxItem":
				case "ToolboxItemAttribute":
				case "System.ComponentModel.ToolboxItem":
				case "System.ComponentModel.ToolboxItemAttribute":
					break;
				default:
					continue;
				}
				var pargs = at.GetPositionalArguments (ctx);
				if (pargs != null && pargs.Count > 0) {
					var val = pargs[0].GetValue (ctx);
					if (val == null)
						return false;
					else if (val is bool)
						return (bool) val;
					else 
						return val != null;
				}
			}

			foreach (var bt in cls.BaseTypes) {
				var bcls = bt.Resolve (ctx).GetDefinition ();
				if (bcls != null && bcls.Kind != TypeKind.Interface)
					return IsToolboxWidget (bcls);
			}

			return false;
		}
		
		static string[] supported_types = new string[] {
			"System.Boolean",
			"System.Char",
			"System.SByte",
			"System.Byte",
			"System.Int16",
			"System.UInt16",
			"System.Int32",
			"System.UInt32",
			"System.Int64",
			"System.UInt64",
			"System.Decimal",
			"System.Single",
			"System.Double",
			"System.DateTime",
			"System.String",
			"System.TimeSpan",
			"Gtk.Adjustment",
		};
	}	
}
