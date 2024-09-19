using System;
using System.Collections.Generic;

namespace Tellinclam.MO
{
    // Port is an extension of the Tuple<string, string> type
    // this class can be simplified
    public class Port
    {
        public string Name { get; set; }
        public string parent { get; set; }
        public List<Port> to { get; set; }
        public Port(string name, string parent)
        {
            Name = $"{parent}.{name}";
            to = new List<Port>();
            this.parent = parent;
        }
        // this mutual assignment indicates a bad data structure. fix it later
        // better to be impolemented by graph model, only count the edge
        public static void Connect(Port prtA, Port prtB)
        {
            prtA.to.Add(prtB);
            prtB.to.Add(prtA);
            return;
        }
        public static void Reconnect(Port prtA, Port prtB)
        {
            prtA.to = new List<Port>() { prtB };
            prtB.to = new List<Port>() { prtA };
            return;
        }
        public string Serialize()
        {
            string scripts = "";
            foreach (Port port in to)
                scripts += $"connect({Name}, {port.Name});\n";
            return scripts;
        }
        public override string ToString()
        {
            return Name;
        }
    }

    public class Pulse
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double startTime { get; set; }
        public double amplitude { get; set; }
        public double width { get; set; }
        public double period { get; set; }
        public double offset { get; set; }
        public Port y { get; set; }
        public Pulse(string name, double startTime, double period, double width, double amplitude, double offset)
        {
            Name = name;
            Path = "Modelica.Blocks.Sources.Pulse";
            this.startTime = startTime;
            this.amplitude = amplitude;
            this.period = period;
            this.width = width;
            if (width > 1)
                this.width = width;
            else
                this.width = Math.Round(100 * width, 1);
            this.offset = offset;
            y = new Port("y", Name);
        }
        public string Serialize()
        {
            return $"{Path} {Name}(startTime={startTime}, period={period}, width={width}, amplitude={amplitude}, offset={offset});\n";
        }
    }


    public class Building
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string idf { get; set; }
        public string epw { get; set; }
        public string wea { get; set; }
        public Port weaBus { get; set; }
        public Building(string idf, string epw, string wea)
        {
            Name = "building";
            Path = "Buildings.ThermalZones.EnergyPlus_9_6_0.Building";
            this.idf = idf;
            this.epw = epw;
            this.wea = wea;
            weaBus = new Port("weaBus", Name);
        }

        public string Serialize()
        {
            string scripts = "";
            // initiation
            scripts += $"inner {Path} {Name}(\n";
            scripts += $"  idfName = Modelica.Utilities.Files.loadResource(\"{idf}\"), \n";
            scripts += $"  weaName = Modelica.Utilities.Files.loadResource(\"{wea}\"), \n";
            scripts += $"  epwName = Modelica.Utilities.Files.loadResource(\"{epw}\"), \n";
            scripts += "  computeWetBulbTemperature = false);\n";
            return scripts;
        }
    }

    public class MixingVolume
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double m_flow_nominal { get; set; }
        public double volume { get; set; }
        public Port[] ports { get; set; }
        public Port heatPort { get; set; }
        public MixingVolume(string name, string medium, int num, double m_flow_nominal, double volume)
        {
            Name = name;
            Path = "Buildings.Fluid.MixingVolumnes.MixingVolume";
            this.medium = medium;
            this.m_flow_nominal = m_flow_nominal;
            ports = new Port[num];
            heatPort = new Port("heatPort", $"{name}");
            for (int i = 0; i < num; i++)
                ports[i] = new Port($"ports[{i + 1}]", $"{name}");
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(nPorts={ports.Length}, m_flow_nominal={m_flow_nominal}, V={volume}, " +
                $"redeclare package Medium={medium});\n";
            return scripts;
        }
    }

    public class MassFlowSource
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double m_flow { get; set; }
        public Port weaBus { get; set; }
        public List<Port> ports { get; set; }
        public MassFlowSource(string name, string medium, double m_flow)
        {
            Name = name;
            Path = "Buildings.Fluid.Sources.MassFlowSource_WeatherData";
            this.medium = medium;
            this.m_flow = m_flow;
            weaBus = new Port("weaBus", Name);
            ports = new List<Port>() {};
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium={medium}, m_flow={m_flow:0.000000}, nPorts={ports.Count});\n";
            return scripts;
        }
    }

    public class FixedTemperature
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double T { get; set; }
        public Port port { get; set; }
        public FixedTemperature(string name, double temp)
        {
            Name = name;
            Path = "Buildings.HeatTransfer.Sources.FixedTemperature";
            T = temp;
            port = new Port("port", $"{name}");
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(T={T});\n";
            return scripts;
        }
    }

    public class MassFlowRate
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double param { get; set; }
        public MassFlowRate(string name, double param)
        {
            Name = name;
            Path = "Modelica.Units.SI.MassFlowRate";
            this.param = param;
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"parameter {Path} {Name} = {param};\n";
            return scripts;
        }
    }

    public class ThermalZone
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public string zoneName { get; set; }
        public double volume { get; set; }
        public Constant qIntGai { get; set; }
        public Port qGai_flow { get; set; }
        public Port TAir { get; set; }
        public Port[] ports { get; set; }
        public MassFlowSource leak { get; set; }
        public PressureDrop crack { get; set; }
        public ThermalZone(string name, string medium, string zoneName, double volume, double q_gain)
        {
            Name = name;
            Path = "Buildings.ThermalZones.EnergyPlus_9_6_0.ThermalZone";
            this.medium = medium;
            this.zoneName = zoneName;
            this.volume = volume;
            qIntGai = new Constant($"{name}_qGai", q_gain);
            qGai_flow = new Port("qGai_flow", name);
            TAir = new Port("TAir", name);
            ports = new Port[4];
            // leave MassFlowRate to be null. append them later
        }

        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium={medium}, zoneName=\"{zoneName}\", nPorts={ports.Length}); \n";
            return scripts;
        }
    }

    public class Boundary_pT
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double T { get; set; }
        public double p { get; set; }
        public bool use_p_in { get; set; }
        public bool use_T_in { get; set; }
        public Port[] ports { get; set; }
        public Port p_in { get; set; }
        public Port T_in { get; set; }
        public Boundary_pT(string name, string medium, int num,
            bool use_T_in = false, bool use_p_in = false, double T = 293.15, double p = 101325)
        {
            Name = name;
            Path = "Buildings.Fluid.Sources.Boundary_pT";
            this.medium = medium;
            this.T = T;
            this.p = p;
            this.use_T_in = use_T_in;
            this.use_p_in = use_p_in;
            ports = new Port[num];
            for (int i = 0; i < num; i++)
                ports[i] = new Port($"ports[{i + 1}]", $"{name}");
            if (use_p_in) p_in = new Port($"p_in", $"{name}");
            if (use_T_in) T_in = new Port($"T_in", $"{name}");
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium={medium}, nPorts={ports.Length}";
            if (use_p_in) scripts += $", use_p_in=true"; else scripts += $", p={p}";
            if (use_T_in) scripts += $", use_T_in=true"; else scripts += $", T={T}";
            scripts += $");\n";
            return scripts;
        }
    }

    public class PressureDrop
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public bool allowFlowReversal { get; set; }
        public bool linearized { get; set; }
        public bool from_dp { get; set; }
        public double dp_nominal { get; set; }
        public double m_flow_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public PressureDrop(string name, string medium, double dp_nominal, double m_flow_nominal,
            bool isReversal = true, bool isLinearized = false, bool from_dp = false)
        {
            Name = name;
            Path = "Buildings.Fluid.FixedResistances.PressureDrop";
            this.medium = medium;
            this.dp_nominal = dp_nominal;
            this.m_flow_nominal = m_flow_nominal;
            this.allowFlowReversal = isReversal;
            this.linearized = isLinearized;
            this.from_dp = from_dp;
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
        }

        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(dp_nominal = {dp_nominal:0.000}, m_flow_nominal = {m_flow_nominal:0.00000000}, \n  " +
                $"redeclare package Medium={ medium }, " +
                $"allowFlowReversal = {allowFlowReversal.ToString().ToLower()}, " +
                $"linearized = {linearized.ToString().ToLower()}, " +
                $"from_dp = {from_dp.ToString().ToLower()});\n";
            return scripts;
        }
    }

    public class Junction
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public bool linearized { get; set; }
        public bool from_dp { get; set; }
        public bool isDynamic { get; set; }
        public double dp_nominal { get; set; }
        public double m_flow_nominal { get; set; }
        public double[] flows { get; set; }
        public double[] res { get; set; }
        public string extVars { get; set; }
        public Port port_1 { get; set; }
        public Port port_2 { get; set; }
        public Port port_3 { get; set; }
        public int outPort { get; set; }
        public Junction(string name, string medium, bool isLinearized, bool isDynamic, 
            bool from_dp, double dp_nominal, double m_flow_nominal, double[] res, double[] flows, string extVars="")
        {
            Name = name;
            Path = "Buildings.Fluid.FixedResistances.Junction";
            this.medium = medium;
            this.linearized = isLinearized;
            this.from_dp = from_dp;
            this.isDynamic = isDynamic;
            this.dp_nominal = dp_nominal;
            this.m_flow_nominal = m_flow_nominal;
            this.res = res;
            this.flows = flows;
            this.extVars = extVars;
            port_1 = new Port("port_1", name); // typically inlet
            port_2 = new Port("port_2", name); // typically outlet
            port_3 = new Port("port_3", name); // typically inlet/outlet
            outPort = 0;
        }
        public string Serialize()
        {
            string scripts = "";
            // presuming the pressure drop of the main inlet has been covered by the upstream
            scripts += $"{Path} {Name}(from_dp={from_dp.ToString().ToLower()}, " +
                //$"dp_nominal = {{0, {dp_nominal}, {dp_nominal * Math.Pow(res[1], 2) / Math.Pow(res[2], 2)}}}, " +
                $"dp_nominal={{{res[0]:0.000}, {res[1]:0.000}, {res[2]:0.000}}}, " +
                $"m_flow_nominal={{{flows[0]:0.000000}, {flows[1]:0.000000}, {flows[2]:0.000000}}}, \n  " +
                $"redeclare package Medium={medium}, " +
                $"linearized={linearized.ToString().ToLower()}, ";
            if (isDynamic)
                scripts += "energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial";
            else
                scripts += "energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState";
            if (extVars != "")
                scripts += $", {extVars}";
            scripts += ");\n";
            return scripts;
        }
    }

    public class TwoWayValve
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public string type { get; set; }
        public double dpFixed_nominal { get; set; }
        public double m_flow_nominal { get; set; }
        public double dpValve_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port y { get; set; }
        public TwoWayValve(string name, string type, string medium, double m_flow_nominal,
            double dpFixed_nominal, double dpValve_nominal)
        {
            Name = name;
            Path = $"Buildings.Fluid.Actuators.Valves.{type}";
            this.medium = medium;
            this.dpFixed_nominal = dpFixed_nominal;
            this.m_flow_nominal = m_flow_nominal;
            this.dpValve_nominal = dpValve_nominal;
            port_a = new Port("port_a", name); // typically inlet
            port_b = new Port("port_b", name); // typically outlet
            y = new Port("y", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(m_flow_nominal={m_flow_nominal:0.000000}, " +
                $"dpFixed_nominal={dpFixed_nominal}, dpValve_nominal={dpValve_nominal}, \n  " +
                $"redeclare package Medium={medium});\n";
            return scripts;
        }
    }

    public class Damper
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double dpFixed_nominal { get; set; }
        public double m_flow_nominal { get; set; }
        public double dpDamper_nominal { get; set; }
        public bool from_dp { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port y { get; set; }
        public Damper(string name, string medium, double m_flow_nominal,
            double dpFixed_nominal, double dpDamper_nominal)
        {
            Name = name;
            Path = "Buildings.Fluid.Actuators.Dampers.Exponential";
            this.medium = medium;
            this.dpFixed_nominal = dpFixed_nominal;
            this.m_flow_nominal = m_flow_nominal;
            this.dpDamper_nominal = dpDamper_nominal;
            port_a = new Port("port_a", name); // typically inlet
            port_b = new Port("port_b", name); // typically outlet
            y = new Port("y", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(m_flow_nominal={m_flow_nominal:0.000000}, " +
                $"dpFixed_nominal={dpFixed_nominal}, dpDamper_nominal={dpDamper_nominal}, \n  " +
                $"redeclare package Medium={medium});\n";
            return scripts;
        }
    }

    public class ThreeWayValve
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double m_flow_nominal { get; set; }
        public double dpValve_nominal { get; set; }
        public double[] dpFixed_nominal { get; set; }
        public bool from_dp { get; set; }
        public Port port_1 { get; set; }
        public Port port_2 { get; set; }
        public Port port_3 { get; set; }
        public Port y { get; set; }
        public ThreeWayValve(string name, string medium, bool from_dp,
            double m_flow_nominal, double[] dpFixed_nominal, double dpValve_nominal)
        {
            Name = name;
            Path = "Buildings.Fluid.Actuators.Valves.ThreeWayLinear";
            this.medium = medium;
            this.from_dp = from_dp;
            this.m_flow_nominal = m_flow_nominal;
            this.dpFixed_nominal = dpFixed_nominal;
            this.dpValve_nominal = dpValve_nominal;
            port_1 = new Port("port_1", name);
            port_2 = new Port("port_2", name);
            port_3 = new Port("port_3", name);
            y = new Port("y", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(m_flow_nominal={m_flow_nominal}, " +
                $"dpFixed_nominal={{{dpFixed_nominal[0]}, {dpFixed_nominal[1]}}}, dpValve_nominal={dpValve_nominal}, \n  " +
                $"redeclare package Medium={medium}, from_dp ={from_dp.ToString().ToLower()}, \n  " + 
                $"energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            return scripts;
        }
    }

    public class Constant
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double k { get; set; }
        public Port y { get; set; }
        public Constant(string name, double k)
        {
            Name = name;
            Path = "Modelica.Blocks.Sources.Constant";
            this.k = k;
            y = new Port("y", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}[3](each k = {k});\n";
            return scripts;
        }
    }

    /// <summary>
    /// type-0: Fan with m_flow_in Real input as continuous flow control
    /// type-1: Pump with stage in Integer input as on/off flow control
    /// </summary>
    public class MoverFlowControlled
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double m_flow_nominal { get; set; }
        public double dp_nominal { get; set; }
        // if type == 0, for continuous input as fan (medium air)
        // if type == 1, for stage input as pump (medium water)
        public int type { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        // optional for Real input, variable flowrate control
        public Port m_flow_in { get; set; }
        // optional for Integer input on/off stage
        public Port stage { get; set; }
        public MoverFlowControlled(string name, string medium, int type, double m_flow_nominal, double dp_nominal)
        {
            Name = name;
            Path = "Buildings.Fluid.Movers.FlowControlled_m_flow";
            this.medium = medium;
            this.type = type;
            this.m_flow_nominal = m_flow_nominal;
            this.dp_nominal = dp_nominal;
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
            m_flow_in = new Port("m_flow_in", name);
            stage = new Port("stage", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(m_flow_nominal={m_flow_nominal:0.000000}, dp_nominal={dp_nominal}, redeclare package Medium={medium}";
            if (type == 0)
                scripts += ", \n  nominalValuesDefineDefaultPressureCurve=true, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState";
            if (type == 1) // .Dynamics.FixedInitial is more complicated
                scripts += ", \n  inputType=Buildings.Fluid.Types.InputType.Stages, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState" +
                    ", redeclare Buildings.Fluid.Movers.Data.Pumps.Wilo.VeroLine80slash115dash2comma2slash2 per";
            scripts += ");\n";
            return scripts;
        }
    }

    public class MoverSpeedControlled
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double m_flow_nominal { get; set; }
        public double dp_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public MoverSpeedControlled(string name, string medium, double m_flow_nominal, double dp_nominal)
        {
            Name = name;
            Path = "Buildings.Fluid.Movers.Preconfigured.SpeedControlled_y";
            this.medium = medium;
            this.m_flow_nominal = m_flow_nominal;
            this.dp_nominal = dp_nominal;
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(m_flow_nominal={m_flow_nominal:0.000000}, dp_nominal={dp_nominal}, \n" +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial);\n";
            return scripts;
        }
    }

    public class Exchangers
    {
        public class Heater_T
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string medium { get; set; }
            public int type { get; set; }
            public double flow_nominal { get; set; }
            public double dp_nominal { get; set; }
            public double q_nominal { get; set; }
            public Port port_a { get; set; }
            public Port port_b { get; set; }
            public Port set { get; set; }
            public Heater_T(string name, string medium, int type, double flow_nominal, double dp_nominal, double q_nominal)
            {
                Name = name;
                this.medium = medium;
                this.flow_nominal = flow_nominal;
                this.dp_nominal = dp_nominal;
                this.q_nominal = q_nominal;
                port_a = new Port("port_a", name);
                port_b = new Port("port_b", name);
                if (type == 0)
                {
                    Path = "Buildings.Fluid.HeatExchangers.Heater_T";
                    this.type = 1;
                    set = new Port("TSet", name);
                }
                else if (type == 1)
                {
                    Path = "Buildings.Fluid.HeatExchangers.HeaterCooler_u";
                    this.type = 2;
                    set = new Port("u", name);
                }
                else
                {
                    this.type = -1;
                }
            }
            public string Serialize()
            {
                // you have to redeclare the medium, or, the equations will not be in line with the rest of the loop
                if (type == 1)
                {
                    string scripts = "";
                    scripts += $"{Path} {Name}(redeclare package Medium={medium}, " +
                        $"m_flow_nominal={flow_nominal:0.000}, dp_nominal={dp_nominal:0.000}, tau=0, show_T=true);\n";
                    return scripts;
                }
                else if (type == 2)
                {
                    string scripts = "";
                    scripts += $"{Path} {Name}(redeclare package Medium={medium}, " +
                        $"m_flow_nominal={flow_nominal:0.000}, dp_nomina={dp_nominal:0.000}, Q_flow_nominal={q_nominal:0.000}, " +
                        $"energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
                    // for static model, use Dynamics.SteadyState. Choose this if you are not confident with the whole system control
                    // for dynamic model, use Dynamics. SteadyStateInitial
                    return scripts;
                }
                else
                {
                    return "--ERROR-- No type definition for exchanger serialization\n";
                }
            }
        }

        public class PrescribedOutlet
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string medium { get; set; }
            public double m_flow_nominal { get; set; }
            public double dp_nominal { get; set; }
            public Port port_a { get; set; }
            public Port port_b { get; set; }
            public Port TSet { get; set; }
            public PrescribedOutlet(string name, string medium,
                double m_flow_nominal, double dp_nominal)
            {
                Name = name;
                Path = "Buildings.Fluid.HeatExchangers.PrescribedOutlet";
                this.medium = medium;
                this.m_flow_nominal = m_flow_nominal;
                this.dp_nominal = dp_nominal;
                port_a = new Port("port_a", $"{name}");
                port_b = new Port("port_b", $"{name}");
                TSet = new Port("TSet", $"{name}");
            }
            public string Serialize()
            {
                string scripts = "";
                scripts += $"{Path} {Name}(m_flow_nominal={m_flow_nominal}, dp_nominal={dp_nominal}, " +
                    $"redeclare package Medium={medium}, use_X_wSet=false);\n";
                return scripts;
            }
        }

        public class CoilEffectivenessNTU
        {
            public int type { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
            public string medium1 { get; set; }
            public string medium2 { get; set; }
            public double m1_flow_nominal { get; set; }
            public double m2_flow_nominal { get; set; }
            public double dp1_nominal { get; set; }
            public double dp2_nominal { get; set; }
            public double Q_flow_nominal { get; set; }
            public double T_a1_nominal { get; set; }
            public double T_a2_nominal { get; set; }
            public double w_a2_nominal { get; set; }
            public bool allowFlowReversal1 { get; set; }
            public bool allowFlowReversal2 { get; set; }
            public Port port_a1 { get; set; }
            public Port port_b1 { get; set; }
            public Port port_a2 { get; set; }
            public Port port_b2 { get; set; }

            public CoilEffectivenessNTU(int type, string name, string medium1, string medium2, 
                double m1_flow_nominal, double m2_flow_nominal, double dp1_nominal, double dp2_nominal,
                double Q_flow_nominal, double T_a1_nominal, double T_a2_nominal, double w_a2_nominal=0.017, bool rev1=true, bool rev2=true)
            {
                this.type = type;
                Name = name;
                if (type == 0)
                    Path = "Buildings.Fluid.HeatExchangers.DryCoilEffectivenessNTU";
                if (type == 1)
                    Path = "Buildings.Fluid.HeatExchangers.WetCoilEffectivenessNTU";
                this.medium1 = medium1;
                this.medium2 = medium2;
                this.m1_flow_nominal = m1_flow_nominal;
                this.m2_flow_nominal = m2_flow_nominal;
                this.dp1_nominal = dp1_nominal;
                this.dp2_nominal = dp2_nominal;
                this.Q_flow_nominal = Q_flow_nominal;
                this.T_a1_nominal = T_a1_nominal;
                this.T_a2_nominal = T_a2_nominal;
                this.w_a2_nominal = w_a2_nominal;
                port_a1 = new Port("port_a1", $"{name}");
                port_b1 = new Port("port_b1", $"{name}");
                port_a2 = new Port("port_a2", $"{name}");
                port_b2 = new Port("port_b2", $"{name}");
            }
            public string Serialize()
            {
                string scripts = "";
                scripts += $"{Path} {Name}(use_Q_flow_nominal=true, show_T=true, \n  " +
                    $"m1_flow_nominal={m1_flow_nominal:0.000000}, m2_flow_nominal={m2_flow_nominal:0.000000}, dp1_nominal={dp1_nominal:0}, dp2_nominal={dp2_nominal:0}, \n  " +
                    $"Q_flow_nominal={Q_flow_nominal:0.000000}, T_a1_nominal={T_a1_nominal:0.0}, T_a2_nominal={T_a2_nominal:0.0},\n  " +
                    $"redeclare package Medium1={medium1}, redeclare package Medium2={medium2}";
                if (type == 1) // define wet air property additionally
                    scripts += $", w_a2_nominal={w_a2_nominal}";
                scripts += ");\n";
                return scripts;
            }
        }
    }

    public class SensorT
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double flow_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port T { get; set; }
        public SensorT(string name, string medium, double flow_nominal)
        {
            Name = name;
            this.medium = medium;
            this.flow_nominal = flow_nominal;
            Path = "Buildings.Fluid.Sensors.TemperatureTwoPort";
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
            T = new Port("T", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium={medium}, m_flow_nominal={flow_nominal});\n";
            return scripts;
        }
    }

    public class SensorF
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public double flow_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port V_flow { get; set; }
        public SensorF(string name, string medium, double flow_nominal)
        {
            Name = name;
            this.medium = medium;
            this.flow_nominal = flow_nominal;
            Path = "Buildings.Fluid.Sensors.VolumeFlowRate";
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
            V_flow = new Port("V_flow", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium={medium}, m_flow_nominal={flow_nominal});\n";
            return scripts;
        }
    }

    public class SensorP
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port p_rel { get; set; }
        public SensorP(string name, string medium)
        {
            Name = name;
            this.medium = medium;
            Path = "Buildings.Fluid.Sensors.RelativePressure";
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
            p_rel = new Port("p_rel", name);
        }
        public string Serialize()
        {
            return $"{Path} {Name}(redeclare package Medium={medium});\n";
        }
    }

    public class Examples
    {
        public class VAVReheatBox
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public double volume { get; set; }
            public double mCooAir_flow_nominal { get; set; }
            public double mHeaAir_flow_nominal { get; set; }
            public bool allowReversal { get; set; }
            public double THeaWatInl_nominal {get;set;}
            public double THeaWatOut_nominal {get;set; }
            public double THeaAirInl_nominal { get; set; }
            public double THeaAirDis_nominal { get; set; }
            public Port port_aAir { get; set; }
            public Port port_bAir { get; set; }
            public Port port_aHeaWat { get; set; }
            public Port port_bHeaWat { get; set; }
            public Port yVAV { get; set; }
            public Port yHea { get; set; }
            public Port y_actual { get; set; }
            public Port VSup_flow { get; set; }

            public VAVReheatBox(string name, double volume, double mCooAirflow, double mHeaAirflow, bool allowReversal,
                double TWaterIn, double TWaterOut, double TAirIn, double TAirOut)
            {
                Name = name;
                Path = "Buildings.Examples.VAVReheat.BaseClasses.VAVReheatBox";
                this.volume = volume;
                mCooAir_flow_nominal = mCooAirflow;
                mHeaAir_flow_nominal = mHeaAirflow;
                this.allowReversal = allowReversal;
                THeaWatInl_nominal = TWaterIn;
                THeaWatOut_nominal = TWaterOut;
                THeaAirInl_nominal = TAirIn;
                THeaAirDis_nominal = TAirOut;
                port_aAir = new Port("port_aAir", name);
                port_bAir = new Port("port_bAir", name);
                port_aHeaWat = new Port("port_aHeaWat", name);
                port_bHeaWat = new Port("port_bHeaWat", name);
                yVAV = new Port("yVAV", name);
                yHea = new Port("yHea", name);
                y_actual = new Port("y_actual", name);
                VSup_flow = new Port("VSup_flow", name);
            }
            public string Serialize()
            {
                string scripts = "";
                scripts += $"{Path} {Name}(redeclare package MediumA=MediumA, redeclare package MediumW=MediumW, VRoo={volume}, " +
                    $"  allowFlowReversal={allowReversal.ToString().ToLower()}, \n" +
                    $"  mCooAir_flow_nominal={mCooAir_flow_nominal}, mHeaAir_flow_nominal={mHeaAir_flow_nominal}, \n" +
                    $"  THeaWatInl_nominal={THeaWatInl_nominal}, THeaWatOut_nominal={THeaWatOut_nominal}, \n" +
                    $"  THeaAirInl_nominal={THeaAirInl_nominal}, THeaAirDis_nominal={THeaAirDis_nominal});\n";
                return scripts;
            }
        }
        public class RoomVAV
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool have_preIndDam { get; set; }
            public double ratVFloMin { get; set; }
            public double ratVFloHea { get; set; }
            public double V_flow_nominal { get; set; }
            public Port TRoo { get; set; }
            public Port TRooHeaSet { get; set; }
            public Port TRooCooSet { get; set; }
            public Port yDam { get; set; }
            public Port yVal { get; set; }
            public Port VDis_flow { get; set; }

            public RoomVAV(string name, bool have_preIndDam, double cooAirflow, double heaAirflow, double outAirflow)
            {
                Name = name;
                Path = "Buildings.Examples.VAVReheat.BaseClasses.Controls.RoomVAV";
                this.have_preIndDam = have_preIndDam;
                this.V_flow_nominal = cooAirflow / 1.2;
                this.ratVFloHea = heaAirflow / cooAirflow;
                this.ratVFloMin = Math.Max(0.15, 1.5 * outAirflow / cooAirflow / 1.2);
                TRoo = new Port("TRoo", name);
                TRooHeaSet = new Port("TRooHeaSet", name);
                TRooCooSet = new Port("TRooCooSet", name);
                yDam = new Port("yDam", name);
                yVal = new Port("yVal", name);
                VDis_flow = new Port("VDis_flow", name);
            }
            public string Serialize()
            {
                string script = "";
                script += $"{Path} {Name}(have_preIndDam={have_preIndDam.ToString().ToLower()}, " +
                    $"ratVFloMin={ratVFloMin}, ratVFloHea={ratVFloHea}, V_flow_nominal={V_flow_nominal});\n";
                return script; 
            }
        }

        public class FanVFD
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public double xSet_nominal { get; set; }
            public double r_N_min { get; set; }
            public Port u { get; set; }
            public Port y { get; set; }
            public Port u_m { get; set; }
            public Port uFan { get; set; }
            public FanVFD(string name, double xSet_nominal, double r_N_min)
            {
                Name = name;
                Path = "Buildings.Examples.VAVReheat.BaseClasses.Controls.FanVFD";
                this.xSet_nominal = xSet_nominal;
                this.r_N_min = r_N_min;
                u = new Port("u", name);
                y = new Port("y", name);
                u_m = new Port("u_m", name);
                uFan = new Port("uFan", name);
            }
            public string Serialize()
            {
                return $"{Path} {Name}(xSet_nominal={xSet_nominal}, r_N_min={r_N_min});\n";
            }
        }
    }

    // pending for update
    public class Documentation
    {
        public string Info { get; set; }
        public double StartTime { get; set; }
        public double StopTime { get; set; }
        public double Interval { get; set; }
        public double Tolerance { get; set; }
        public string Algorithm { get; set; }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"annotation(\n" + 
                $"Documentation(info = \"{Info}\"),\n" + 
                $"experiment(StartTime = {StartTime}, StopTime = {StopTime}, Interval = {Interval}, Tolerance = {Tolerance}));\n";
            return scripts;
        }
    }

    // embedded blocks
    public class MassAverage
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double[] weights { get; set; }
        public Port[] u { get; set; }
        public Port y { get; set; }
        public MassAverage(string name, int num, double[] weights)
        {
            Name = name;
            Path = "MassAverage";
            this.weights = weights;
            u = new Port[num];
            y = new Port("y", $"{name}");
            for (int i = 0; i < num; i++)
            {
                u[i] = new Port($"u[{i + 1}]", $"{name}");
            }
        }
        public string Serialize()
        {
            string factors = "";
            for (int i = 0; i < weights.Length; i++)
            {
                factors += $"{weights[i]:0.00}";
                if (i != weights.Length - 1)
                    factors += ", ";
            }
            string scripts = "";
            scripts += $"{Path} {Name}(n = {u.Length}, w = {{{factors}}});\n";
            return scripts;
        }
    }
}
