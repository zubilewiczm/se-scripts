using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ObjectBuilders.VisualScripting;
using VRageMath;
using VRageRender;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        static readonly string arg = "Platform Screen H2/O2";
        static readonly string postfix = "Prod";

        private readonly List<ProgramInstance> m_screens;
        private readonly List<IMyGasTank> m_tanksO2;
        private readonly List<IMyGasTank> m_tanksH2;

        public List<IMyGasTank> TanksO2 { get { return m_tanksO2; } }
        public List<IMyGasTank> TanksH2 { get { return m_tanksH2; } }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            m_screens = new List<ProgramInstance>(
                GetAllTextSurfacesWithSection(arg)
                    .Select(x => new ProgramInstance(this, x))
                );
            m_tanksO2 = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(m_tanksO2, x => x.CustomName.StartsWith($"Oxygen Tank {postfix}"));

            m_tanksH2 = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(m_tanksH2, x => x.CustomName.StartsWith($"Hydrogen Tank {postfix}"));
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateType)
        {
            foreach (var s in m_screens)
            {
                s.Main();
            }
        }

        private class ProgramInstance
        {
            private Program m_parent;
            private IMyTextSurface m_ts;
            private RectangleF m_viewport;

            float Padding { get; set; } = 15f;

            public ProgramInstance(Program parent, IMyTextSurface ts)
            {
                m_ts = ts;
                m_viewport = CalculateDrawSurfaceBounds(ts);
                m_parent = parent;
            }

            public void Main()
            {
                var drawframe = m_ts.DrawFrame();
                Draw(drawframe);
                drawframe.Dispose();
            }

            public void Draw(MySpriteDrawFrame drawframe)
            {
                var pos = new Vector2(m_viewport.X + Padding, m_viewport.Y + Padding);

                var bbox_names  = WriteColumn(drawframe, pos,
                    x => x.CustomName,
                    (l, s) => $"  Total {s}"
                    );

                var pos_c2 = new Vector2(bbox_names.Right + 50.0f, bbox_names.Y);
                var bbox_stored = WriteColumn(drawframe, pos_c2,
                    x => (x.FilledRatio * x.Capacity).ToString("# ### ### 000.00"),
                    (l, s) => l.Select(x => x.FilledRatio * x.Capacity).Sum().ToString("# ### ### 000.00")
                    );

                var pos_c3 = new Vector2(bbox_stored.Right + 10.0f, bbox_names.Y);
                var bbox_slash = WriteColumn(drawframe, pos_c3,
                    x => "/",
                    (l, s) => "/"
                    );

                var pos_c4 = new Vector2(bbox_slash.Right + 10.0f, bbox_names.Y);
                var bbox_cap = WriteColumn(drawframe, pos_c4,
                    x =>  x.Capacity.ToString("# ### ### 000") + "L",
                    (l, s) => l.Select(x => x.Capacity).Sum().ToString("# ### ### 000") + " L"
                    );
            }

            public RectangleF WriteColumn(
                MySpriteDrawFrame drawframe,
                Vector2 pos,
                Func<IMyGasTank, string> per_tank,
                Func<List<IMyGasTank>, string, string> agg_val
                )
            {
                var renderer = new TextRenderer(m_ts, drawframe, pos)
                {
                    Scale = 0.5f,
                    Interline = 1.0f
                };
                foreach (var t in m_parent.TanksO2)
                {
                    renderer.WriteLine(per_tank(t));
                }
                renderer.VSpace(10.0f);
                renderer.Scale = 0.7f;
                renderer.WriteLine(agg_val(m_parent.TanksO2, "O2"));
                renderer.VSpace(20.0f);

                renderer.Scale = 0.5f;
                foreach (var t in m_parent.TanksH2)
                {
                    renderer.WriteLine(per_tank(t));
                }
                renderer.VSpace(10.0f);
                renderer.Scale = 0.7f;
                renderer.WriteLine(agg_val(m_parent.TanksH2, "H2"));
                return renderer.BBox;
            }

            public static RectangleF CalculateDrawSurfaceBounds(IMyTextSurface surf)
            {
                return new RectangleF(
                    (surf.TextureSize - surf.SurfaceSize) / 2f,
                    surf.SurfaceSize
                );
            }
        }

        public IEnumerable<IMyTextSurface> GetAllTextSurfacesWithSection(string section)
        {
            var list_withconf = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(list_withconf, x => MyIni.HasSection(x.CustomData, section));

            var iter_textsurfs = list_withconf.OfType<IMyTextSurface>();
            var iter_tsproviders = list_withconf.OfType<IMyTextSurfaceProvider>();
            var iter_provider_surfs = CompatibleTextSurfaces(iter_tsproviders, section);
            return iter_textsurfs.Concat(iter_provider_surfs);
        }

        public IEnumerable<IMyTextSurface> CompatibleTextSurfaces(IEnumerable<IMyTextSurfaceProvider> tsps, string section)
        {
            foreach (var tsp in tsps)
            {
                var ini = new MyIni();
                var term = tsp as IMyTerminalBlock;
                ini.TryParse(term.CustomData);
                var screens_repr = ini.Get(section, "Which").ToString().Split(',');

                int temp = 0;
                var screens = screens_repr
                    .Select(   x =>
                    {
                        if (Int32.TryParse(x, out temp))
                        {
                            return temp;
                        }
                        else
                        {
                            return -1;
                        }
                    })
                    .Where(   x => x != -1 )
                    .OrderBy( x => x )
                    .Select(  x => tsp.GetSurface(x) )
                    .Where(   x => x != null );
                foreach (var s in screens)
                {
                    yield return s;
                }
            }
        }

        class TextRenderer
        {
            private readonly IMyTextSurface m_surface;
            private MySpriteDrawFrame m_frame;
            private Vector2 m_pos;
            private Vector2 m_init_pos;
            private Vector2 m_edge;

            private string m_font;
            private float m_scale;
            private float m_interline_skip;

            public Vector2 CursorPos { get { return m_pos; } set { m_pos = value; } }

            public RectangleF BBox {
                get {
                    return new RectangleF
                    {
                        X = m_init_pos.X,
                        Y = m_init_pos.Y,
                        Width = m_edge.X - m_init_pos.X,
                        Height = m_edge.Y - m_init_pos.Y
                    };
                }
            }

            public string Font {
                get {
                    return m_font;
                }
                set {
                    m_font = value;
                    CalculateInterlineSkip();
                }
            }
            public float Scale
            {
                get
                {
                    return m_scale;
                }
                set
                {
                    m_scale = value;
                    CalculateInterlineSkip();
                }
            }

            public float Interline { get; set; } = 1.1f;

            public TextRenderer(IMyTextSurface surf, MySpriteDrawFrame frame, Vector2 init_pos)
            {
                m_surface = surf;
                m_frame = frame;
                m_init_pos = init_pos;
                m_pos = init_pos;
                m_edge = init_pos;
                m_font = "White";
                m_scale = 0.8f;
                m_interline_skip = CalculateInterlineSkip();
            }
            public void WriteLine(string line = "", Color? color = null, float? scale = null)
            {
                Write(line, color, scale);
                m_pos.X = m_init_pos.X;
                m_pos.Y += Interline * m_interline_skip;
            }

            public void Write(string line, Color? color = null, float? scale = null)
            {
                if (line.Length > 0)
                {
                    var textSprite = new MySprite
                    {
                        Type = SpriteType.TEXT,
                        Data = line,
                        Position = m_pos,
                        RotationOrScale = scale.HasValue ? scale.Value : Scale,
                        Color = color.HasValue ? color.Value : m_surface.ScriptForegroundColor,
                        Alignment = TextAlignment.LEFT,
                        FontId = Font
                    };
                    m_frame.Add(textSprite);
                    Vector2 textsize = new Vector2
                    {
                        X = m_surface.MeasureStringInPixels(new StringBuilder(line), Font, m_scale).X,
                        Y = Interline * m_interline_skip
                    };
                    m_edge = Vector2.Max(m_edge, m_pos + textsize);
                    m_pos.X += textsize.X;
                }
            }

            public void VSpace(float pixels)
            {
                m_pos.Y += pixels;
            }
            public void HSpace(float pixels)
            {
                m_pos.X += pixels;
            }

            public float CalculateInterlineSkip()
            {
                return m_surface.MeasureStringInPixels(new StringBuilder("|p"), Font, m_scale).Y;
            }
        }
    }
}
