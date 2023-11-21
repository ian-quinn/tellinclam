using Eto.Forms.ThemedControls;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Tellinclam.MO
{
    public class Port
    {
        public string Name { get; set; }
        public string parent { get; set; }
        public Port to { get; set; }
        public Port(string name, string parent)
        {
            Name = $"{parent}.{name}";
            this.parent = parent;
        }
        public static void Connect(Port prtA, Port prtB)
        {
            prtA.to = prtB;
            prtB.to = prtA;
            return;
        }
    }
    public class Pulse
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double amplitude { get; set; }
        public double width { get; set; }
        public double period { get; set; }
        public double shift { get; set; }
        public double offset { get; set; }
        public Port y { get; set; }
        public Pulse(string name, double amplitude, double width,
            double period, double shift, double offset)
        {
            Name = name;
            Path = "Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse";
            this.amplitude = amplitude;
            this.width = width;
            this.period = period;
            this.shift = shift;
            this.offset = offset;
            y = new Port("y", name);
        }
    }

    public class PID
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double k { get; set; }
        public double Ti { get; set; }
        public double Td { get; set; }
        public double yMax { get; set; }
        public double yMin { get; set; }
        public Port u_s { get; set; }
        public Port u_m { get; set; }
        public Port y { get; set; }
        public PID(string name, double k, double Ti,
            double Td, double yMax, double yMin)
        {
            Name = name;
            Path = "Buildings.Controls.OBC.CDL.Continuous.PID";
            this.k = k;
            this.Ti = Ti;
            this.Td = Td;
            this.yMax = yMax;
            this.yMin = yMin;
            u_s = new Port("u_s", name);
            u_m = new Port("u_m", name);
            y = new Port("y", name);
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
            scripts += $"inner {Path} {Name}(idfName = Modelica.Utilities.Files.loadResource(\"{idf}\"), \n";
            scripts += $"weaName = Modelica.Utilities.Files.loadResource(\"{wea}\"), \n";
            scripts += $"epwName = Modelica.Utilities.Files.loadResource(\"{epw}\"), \n";
            scripts += "computeWetBulbTemperature = false);\n";
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
            ports = new List<Port>() { };
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium = {medium}, m_flow = {m_flow:0.00000}, nPorts = {ports.Count});\n";
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
        public string zoneName { get; set; }
        public double volume { get; set; }
        public Constant qIntGai { get; set; }
        public Port qGai_flow { get; set; }
        public Port TAir { get; set; }
        public List<Port> ports { get; set; }
        public double recFlow { get; set; }
        public double outFlow { get; set; }
        public MassFlowSource newFlow { get; set; }
        public PressureDrop crack { get; set; }
        public ThermalZone(string name, string zoneName, double volume, double q_gain)
        {
            Name = name;
            Path = "Buildings.ThermalZones.EnergyPlus_9_6_0.ThermalZone";
            this.zoneName = zoneName;
            this.volume = volume;
            qIntGai = new Constant($"{name}_qGai", q_gain);
            qGai_flow = new Port("qGai_flow", name);
            TAir = new Port("TAir", name);
            ports = new List<Port>() { };
            // leave MassFlowRate to be null. append them later
        }

        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium = Medium, zoneName = \"{zoneName}\", nPorts = {ports.Count}); \n";
            //scripts += this.recFlow.Serialize();
            //scripts += this.outFlow.Serialize();
            //scripts += this.newFlow.Serialize();
            //scripts += this.qIntGai.Serialize();
            //scripts += this.crack.Serialize();
            return scripts;
        }
    }

    public class Boundary
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string medium { get; set; }
        public List<Port> ports { get; set; }
        public Boundary(string name, string medium)
        {
            Name = name;
            Path = "Buildings.Fluid.Sources.Boundary_pT";
            this.medium = medium;
            ports = new List<Port>() { };
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium = {medium}, nPorts = {ports.Count});\n";
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
        public PressureDrop(string name, string medium, bool isReversal, bool isLinearized,
            bool from_dp, double dp_nominal, double m_flow_nominal)
        {
            Name = name;
            Path = "Buildings.Fluid.FixedResistances.PressureDrop";
            this.medium = medium;
            this.allowFlowReversal = isReversal;
            this.linearized = isLinearized;
            this.from_dp = from_dp;
            this.dp_nominal = dp_nominal;
            this.m_flow_nominal = m_flow_nominal;
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium = {medium}, " +
                $"allowFlowReversal = {allowFlowReversal.ToString().ToLower()}, " +
                $"linearized = {linearized.ToString().ToLower()}, " +
                $"from_dp = {from_dp.ToString().ToLower()}, dp_nominal = {dp_nominal:0.000}, " +
                $"m_flow_nominal = {m_flow_nominal:0.000});\n";
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
        public double dp_nominal { get; set; }
        public double m_flow_nominal { get; set; }
        public double[] flows { get; set; }
        public double[] res { get; set; }
        public Port port_1 { get; set; }
        public Port port_2 { get; set; }
        public Port port_3 { get; set; }
        public int outPort { get; set; }
        public Junction(string name, string medium, bool isLinearized,
            bool from_dp, double dp_nominal, double m_flow_nominal, double[] res, double[] flows)
        {
            Name = name;
            Path = "Buildings.Fluid.FixedResistances.Junction";
            this.medium = medium;
            this.linearized = isLinearized;
            this.from_dp = from_dp;
            this.dp_nominal = dp_nominal;
            this.m_flow_nominal = m_flow_nominal;
            this.res = res;
            this.flows = flows;
            port_1 = new Port("port_1", name); // typically inlet
            port_2 = new Port("port_2", name); // typically outlet
            port_3 = new Port("port_3", name); // typically inlet/outlet
            outPort = 0;
        }
        public string Serialize()
        {
            string scripts = "";
            // presuming the pressure drop of the main inlet has been covered by the upstream
            scripts += $"{Path} {Name}(redeclare package Medium = {medium}, " +
                $"linearized = {linearized.ToString().ToLower()}, from_dp = {from_dp.ToString().ToLower()}, " +
                //$"dp_nominal = {{0, {dp_nominal}, {dp_nominal * Math.Pow(res[1], 2) / Math.Pow(res[2], 2)}}}, " +
                $"dp_nominal = {{{res[0]:0.000}, {res[1]:0.000}, {res[2]:0.000}}}, " +
                $"m_flow_nominal = {{{flows[0]:0.000}, {flows[1]:0.000}, {flows[2]:0.000}}}, " + 
                $"energyDynamics = Modelica.Fluid.Types.Dynamics.SteadyState);\n";
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

    // e.g. the Fan
    public class ControlledMassFlow
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double flow_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port m_flow_in { get; set; }
        public ControlledMassFlow(string name, double flow_nominal)
        {
            Name = name;
            Path = "Buildings.Fluid.Movers.FlowControlled_m_flow";
            this.flow_nominal = flow_nominal;
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
            m_flow_in = new Port("m_flow_in", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium = Medium, " +
                $"energyDynamics = Modelica.Fluid.Types.Dynamics.SteadyState, " +
                $"m_flow_nominal = {flow_nominal}, nominalValuesDefineDefaultPressureCurve = true);\n";
            return scripts;
        }
    }

    public class Exchanger
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int type { get; set; }
        public double flow_nominal { get; set; }
        public double dp_nominal { get; set; }
        public double q_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port set { get; set; }
        public Exchanger(string name, int type, double flow_nominal, double dp_nominal, double q_nominal)
        {
            Name = name;
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
                scripts += $"{Path} {Name}(redeclare package Medium = Medium, " +
                    $"m_flow_nominal = {flow_nominal:0.000}, dp_nominal = {dp_nominal:0.000}, tau = 0, show_T = true);\n";
                return scripts;
            }
            else if (type == 2)
            {
                string scripts = "";
                scripts += $"{Path} {Name}(redeclare package Medium = Medium, " +
                    $"m_flow_nominal = {flow_nominal:0.000}, dp_nominal = {dp_nominal:0.000}, Q_flow_nominal = {q_nominal:0.000}, " +
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

    public class SensorT
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double flow_nominal { get; set; }
        public Port port_a { get; set; }
        public Port port_b { get; set; }
        public Port T { get; set; }
        public SensorT(string name, double flow_nominal)
        {
            Name = name;
            this.flow_nominal = flow_nominal;
            Path = "Buildings.Fluid.Sensors.TemperatureTwoPort";
            port_a = new Port("port_a", name);
            port_b = new Port("port_b", name);
            T = new Port("T", name);
        }
        public string Serialize()
        {
            string scripts = "";
            scripts += $"{Path} {Name}(redeclare package Medium = Medium, m_flow_nominal = {flow_nominal});\n";
            return scripts;
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
        public int num { get; set; }
        public double[] weights { get; set; }
        public Port[] u { get; set; }
        public Port y { get; set; }
        public MassAverage(string name, int num, double[] weights)
        {
            Name = name;
            Path = "MassAverage";
            this.num = num;
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
            scripts += $"{Path} {Name}(n = {num}, w = {{{factors}}});\n";
            return scripts;
        }
    }
}
