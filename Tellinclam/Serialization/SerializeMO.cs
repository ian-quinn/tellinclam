using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

using Tellinclam.MO;
using Tellinclam.JSON;

namespace Tellinclam
{
    class Preset
    {
        /// <summary>
        /// with input n-size real inputs and weight factor array, output single output average
        /// </summary>
        public static string WeightedAverage()
        {
            string scripts = "\n";
            scripts += "block MassAverage\n" +
                "  parameter Integer n;\n" +
                "  parameter Real w[n];\n" +
                "  Buildings.Controls.OBC.CDL.Interfaces.RealInput u[n];\n" +
                "  Buildings.Controls.OBC.CDL.Interfaces.RealOutput y;\n" +
                "protected\n" +
                "  Real mass;\n" +
                "algorithm\n" +
                "  mass := 0;\n" +
                "  for i in 1:n loop\n" +
                "    mass := mass + w[i]*u[i];\n" +
                "  end for;\n" +
                "  y := mass / sum(w);\n" +
                "end MassAverage;\n\n";
            return scripts;
        }
        /// <summary>
        /// Entwine n real inputs into single output n-size array
        /// </summary>
        public static string Entwine()
        {
            string scripts = "\n";
            scripts += "block Entwine\n" +
                "  parameter Integer n;\n" +
                "  Buildings.Controls.OBC.CDL.Interfaces.RealInput u[n];\n" +
                "  Buildings.Controls.OBC.CDL.Interfaces.RealOutput[n] y;\n" +
                "algorithm\n" +
                "  for i in 1:n loop\n" +
                "    y[i] := u[i];\n" +
                "  end for;\n" +
                "end Entwine;\n\n";
            return scripts;
        }

        /// <summary>
        /// The original control scheme from Buildings library. As reference.
        /// </summary>
        /*
        public static Tuple<string, string> FanHeating(string id, Port thermostat, Port fan, Port heater, 
             double rec_flow, double minTemp, double gain, double amp, double offset)
        {
            string model = "";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse THig{id}(amplitude = 0, offset = {offset + 273.15}, period = 86400, " +
                $" shift = 25200, width = 0.417, y(displayUnit = \"degC\", unit = \"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse TLow{id}(amplitude = {amp - 0}, offset = 0, period = 86400, " +
                $" shift = 32400, width = 0.375, y(displayUnit = \"degC\", unit = \"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add TSet{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.PID conPID{id}(Ti(displayUnit = \"min\") = 1800, controllerType = Buildings.Controls.OBC.CDL.Types.SimpleController.PI, " +
                $" k = 1, u_m(displayUnit = \"degC\", unit = \"K\"), u_s(displayUnit = \"degC\", unit = \"K\"), yMax = 1, yMin = 0);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Hysteresis staA{id}(uLow = 0.05, uHigh = 0.5);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Hysteresis staB{id}(uLow = 0.5, uHigh = 0.75);\n";
            // two level of speed control
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanA{id}_flow(realTrue = {rec_flow / 2});\n";
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanB{id}_flow(realTrue = {rec_flow / 2});\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add m_fan_set{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add TAirLvgSet{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.MultiplyByParameter gai{id}(final k = {gain});\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.AddParameter TSupMin{id}(p = {minTemp});\n";

            string equation = "";
            // PID controller setting
            equation += $"connect(THig{id}.y, TSet{id}.u1);\n";
            equation += $"connect(TLow{id}.y, TSet{id}.u2);\n";
            equation += $"connect(TSet{id}.y, conPID{id}.u_s);\n";
            equation += $"connect(conPID{id}.u_m, {thermostat.Name});\n";
            equation += $"connect(conPID{id}.y, staA{id}.u);\n";
            equation += $"connect(staA{id}.y, mSetFanA{id}_flow.u);\n";
            equation += $"connect(conPID{id}.y, staB{id}.u);\n";
            equation += $"connect(staB{id}.y, mSetFanB{id}_flow.u);\n";
            equation += $"connect(conPID{id}.y, gai{id}.u);\n";
            // fan controller
            equation += $"connect(mSetFanB{id}_flow.y, m_fan_set{id}.u1);\n";
            equation += $"connect(mSetFanA{id}_flow.y, m_fan_set{id}.u2);\n";
            equation += $"connect(m_fan_set{id}.y, {fan.Name});\n";
            // heater controller
            equation += $"connect(gai{id}.y, TAirLvgSet{id}.u1);\n";
            equation += $"connect(TSupMin{id}.y, TAirLvgSet{id}.u2);\n";
            equation += $"connect(TAirLvgSet{id}.y, {heater.Name});\n";
            equation += $"connect({thermostat.Name}, TSupMin{id}.u);\n";

            return new Tuple<string, string>(model, equation);
        }
        */

        /// <summary>
        /// Template 2-pipe fan coil system with 3-level wind speed control and ideal heat/cool source
        /// </summary>
        public static Tuple<string, string> FanCoilControl(int id, bool isHeatingMode, double[] schedule, double[] setpoint, 
            Port thermostat, Port fan, Port actuator, double rec_flow)
        {
            // by default, preheat starts 1 hour before occupancy schedule
            string module = "";
            //module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse THig_zon{id}(amplitude = 7, offset = 0, " +
            //    $"period = 86400, shift = {schedule[0]*3600}, width = {Math.Round((schedule[1] - schedule[0])/24, 3)}, y(displayUnit = \"degC\", unit = \"K\"));\n";
            //module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse TLow_zon{id}(amplitude = 14, offset = 273.15, " +
            //    $"period = 86400, shift = {(schedule[0]-1)*3600}, width = {Math.Round((schedule[1] - schedule[0] + 1) / 24, 3)}, y(displayUnit = \"degC\", unit = \"K\"));\n";
            double offset = 0;
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse TDay_zon{id}(amplitude={setpoint[0]-offset}, offset=0, " +
                $"period=86400, shift={schedule[0]*3600}, width={Math.Round((schedule[1] - schedule[0])/24, 3)}, y(displayUnit=\"degC\", unit=\"K\"));\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse TYea_zon{id}(amplitude={setpoint[1] + setpoint[0] - 2*offset}, offset={offset} + 273.15, " +
                $"period={365*24*3600}, shift={160.5*24*3600}, width=0.5, y(displayUnit=\"degC\", unit=\"K\"));\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse TFac_zon{id}(amplitude=2, offset=-1, " +
                $"period={365 * 24 * 3600}, shift=-{22 * 24 * 3600}, width=0.5, y(displayUnit=\"degC\", unit=\"K\"));\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Multiply TMul_zon{id};\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Add TSet_zon{id};\n";
            // note here we use modR2B.y port from upper level as mode switch
            module += $"Buildings.Controls.OBC.CDL.Reals.PID conPID_zon{id}(k = 1, Ti = 120, reverseActing={isHeatingMode.ToString().ToLower()}, " +
                $"controllerType = Buildings.Controls.OBC.CDL.Types.SimpleController.PI, u_m(displayUnit=\"degC\", unit=\"K\"), u_s(displayUnit=\"degC\", unit=\"K\"));\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Hysteresis staA_zon{id}(uLow = 0.05, uHigh = 0.5);\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Hysteresis staB_zon{id}(uLow = 0.5, uHigh = 0.75);\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Hysteresis staC_zon{id}(uLow = 0.75, uHigh = 0.95);\n";    
            module += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanA_zon{id}_flow(realTrue = {rec_flow / 2});\n";
            module += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanB_zon{id}_flow(realTrue = {rec_flow / 4});\n";
            module += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanC_zon{id}_flow(realTrue = {rec_flow / 4});\n";
            module += $"Modelica.Blocks.Math.Add3 m_fan_set_zon{id};\n";
            module += $"Modelica.Blocks.Sources.Constant alwayson_zon{id}(k=1);\n";

            string equation = "";
            //equation += $"connect(THig_zon{id}.y, TSet_zon{id}.u1);\n";
            //equation += $"connect(TLow_zon{id}.y, TSet_zon{id}.u2);\n";
            equation += $"connect(TDay_zon{id}.y, TMul_zon{id}.u1);\n";
            equation += $"connect(TFac_zon{id}.y, TMul_zon{id}.u2);\n";
            equation += $"connect(TMul_zon{id}.y, TSet_zon{id}.u1);\n";
            equation += $"connect(TYea_zon{id}.y, TSet_zon{id}.u2);\n";

            equation += $"connect(TSet_zon{id}.y, conPID_zon{id}.u_s);\n";
            equation += $"connect(conPID_zon{id}.u_m, {thermostat.Name});\n";
            equation += $"connect(conPID_zon{id}.y, staA_zon{id}.u);\n";
            equation += $"connect(conPID_zon{id}.y, staB_zon{id}.u);\n";
            equation += $"connect(conPID_zon{id}.y, staC_zon{id}.u);\n";
            // use always on for now
            //equation += $"connect(conPID_zon{id}.y, {actuator.Name});\n"; // valve of exchanger
            equation += $"connect(alwayson_zon{id}.y, {actuator.Name});\n";

            equation += $"connect(staA_zon{id}.y, mSetFanA_zon{id}_flow.u);\n";
            equation += $"connect(staB_zon{id}.y, mSetFanB_zon{id}_flow.u);\n";
            equation += $"connect(staC_zon{id}.y, mSetFanC_zon{id}_flow.u);\n";
            equation += $"connect(mSetFanB_zon{id}_flow.y, m_fan_set_zon{id}.u1);\n";
            equation += $"connect(mSetFanA_zon{id}_flow.y, m_fan_set_zon{id}.u2);\n";
            equation += $"connect(mSetFanC_zon{id}_flow.y, m_fan_set_zon{id}.u3);\n";
            equation += $"connect(m_fan_set_zon{id}.y, {fan.Name});\n";

            return new Tuple<string, string>(module, equation);
        }

        
        /// <summary>
        /// Pending for detail settings.
        /// </summary>
        /// <returns>Tuple.Item1: the module definition scripts <br></br> Tuple.Item2: the equation scripts. <br></br>
        /// These two parts will be serialized in different parts of Modelica scripts</returns>
        public static Tuple<string, string> IdealLoad(string id, double[] setPoint, double[] occSch, 
            double[] heatLoads, double[] coolLoads, Port thermostat, Port[] heatPorts)
        {
            // check if feeded with the correct number of heat ports
            if (heatPorts.Length != heatLoads.Length || heatPorts.Length != coolLoads.Length)
                return null;

            string model = "";
            // use constant setpoint or in line with the actual operation schedule
            //model += $"Buildings.Controls.OBC.CDL.Reals.Sources.Constant THeaSet_{id}(k(final unit=\"K\", displayUnit=\"degC\")={setPoint[0] + 273.15});\n";
            //model += $"Buildings.Controls.OBC.CDL.Reals.Sources.Constant TCooSet_{id}(k(final unit=\"K\", displayUnit=\"degC\")={setPoint[1] + 273.15});\n";

            model += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse THeaSet_{id}(amplitude=50 + {setPoint[0]}, offset=223.15, " +
                $"period=86400, shift={occSch[0] * 3600}, width={Math.Round((occSch[1] - occSch[0]) / 24, 3)}, y(displayUnit=\"degC\", unit=\"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse TCooSet_{id}(amplitude={setPoint[1]} - 100, offset=373.15, " +
                $"period=86400, shift={occSch[0] * 3600}, width={Math.Round((occSch[1] - occSch[0]) / 24, 3)}, y(displayUnit=\"degC\", unit=\"K\"));\n";

            model += $"Buildings.Controls.OBC.CDL.Reals.PID conPID_Hea{id}(final k=1, final Ti=120, final Td=0.1, reverseActing=true, \n  " +
                $"final controllerType=Buildings.Controls.OBC.CDL.Types.SimpleController.PI);\n";
            model += $"Buildings.Controls.OBC.CDL.Reals.PID conPID_Coo{id}(final k=1, final Ti=120, final Td=0.1, reverseActing=false, \n  " +
                $"final controllerType=Buildings.Controls.OBC.CDL.Types.SimpleController.PI);\n";
            for (int i = 0; i < heatLoads.Length; i++)
            {
                model += $"Buildings.Controls.OBC.CDL.Reals.MultiplyByParameter gaiHea{id}_{i}(final k=5*{heatLoads[i]});\n";
                model += $"Buildings.Controls.OBC.CDL.Reals.MultiplyByParameter gaiCoo{id}_{i}(final k=-5*{coolLoads[i]});\n";
                model += $"Modelica.Blocks.Math.Add add{id}_{i};\n";
                model += $"Modelica.Thermal.HeatTransfer.Sources.PrescribedHeatFlow preHeaFlo{id}_{i};\n";
            }

            // toggle this block to use the idealload package of Buildings
            //for (int i = 0; i < heatLoads.Length; i++)
            //{
            //    model += $"Buildings.ThermalZones.EnergyPlus_9_6_0.Examples.SmallOffice.BaseClasses.IdealHeaterCooler hea{id}_{i}(" +
            //        $"Q_flow_nominal={heatLoads[i]});\n";
            //}

            string equation = "";
            // PID controller setting
            equation += $"connect(THeaSet_{id}.y, conPID_Hea{id}.u_s);\n";
            equation += $"connect(TCooSet_{id}.y, conPID_Coo{id}.u_s);\n";
            equation += $"connect(conPID_Hea{id}.u_m, {thermostat});\n";
            equation += $"connect(conPID_Coo{id}.u_m, {thermostat});\n";
            for (int i = 0; i < heatLoads.Length; i++)
            {
                // only toggle this block for equation part for idealload package from Buildings
                //equation += $"connect(hea{id}_{i}.heaPor, {heatPorts[i]});\n";
                //equation += $"connect(hea{id}_{i}.TMea, {thermostat});\n";
                //equation += $"connect(hea{id}_{i}.TSet, THeaSet_{id}.y);\n";

                equation += $"connect(conPID_Hea{id}.y, gaiHea{id}_{i}.u);\n";
                equation += $"connect(conPID_Coo{id}.y, gaiCoo{id}_{i}.u);\n";
                equation += $"connect(gaiHea{id}_{i}.y, add{id}_{i}.u1);\n";
                equation += $"connect(gaiCoo{id}_{i}.y, add{id}_{i}.u2);\n";
                equation += $"connect(add{id}_{i}.y, preHeaFlo{id}_{i}.Q_flow);\n";
                equation += $"connect(preHeaFlo{id}_{i}.port, {heatPorts[i]});\n";
            }
            // output an array of prescribe heatflow modules
            return new Tuple<string, string>(model, equation);
        }

        /// <summary>
        /// hot/chilled water source. ref: Buildings.Fluid.Examples.FlowSystem
        /// </summary>
        public static Tuple<string, string> PumpSource(string medium, double[] setPoint, double[] occSch, 
            out Port sysSupply, out Port sysReturn, out Port pumpChain, out Port valveChain)
        {
            string module = "";
            // heating source
            module += "Buildings.Fluid.Actuators.Valves.TwoWayLinear valHea(m_flow_nominal=10, dpFixed_nominal=0, dpValve_nominal=1e4, \r\n" +
                $"  allowFlowReversal=false, redeclare package Medium={medium}, CvData=Buildings.Fluid.Types.CvTypes.OpPoint);\n";
            module += "Buildings.Fluid.HeatExchangers.PrescribedOutlet heater(m_flow_nominal=10, dp_nominal=100,\r\n  " +
                $"allowFlowReversal=false, redeclare package Medium={medium}, use_X_wSet=false);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl(m_flow_nominal={10,10,10}, dp_nominal={1000,10,10},\r\n  " +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl1(m_flow_nominal={10,10,10}, dp_nominal={10,10,10},\r\n  " +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += "Buildings.Fluid.Movers.FlowControlled_m_flow pmpHea(m_flow_nominal=10, allowFlowReversal=false, \r\n  " +
                $"redeclare package Medium={medium}, redeclare Buildings.Fluid.Movers.Data.Pumps.Wilo.VeroLine80slash115dash2comma2slash2 per,\r\n  " +
                "inputType=Buildings.Fluid.Types.InputType.Stages, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += $"Modelica.Blocks.Sources.Constant Thot(k=273.15 + {setPoint[0]});\n";
            module += $"Buildings.Fluid.Sources.Boundary_pT bou(nPorts=1, redeclare package Medium={medium});\n";
            // cooling source
            module += "Buildings.Fluid.Actuators.Valves.TwoWayLinear valCoo(m_flow_nominal=10, dpFixed_nominal=0, dpValve_nominal=1e4, \r\n  " +
                $"allowFlowReversal=false, redeclare package Medium={medium}, CvData=Buildings.Fluid.Types.CvTypes.OpPoint);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl2(m_flow_nominal={10,10,10}, dp_nominal={1000,10,10},\r\n  " +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl3(m_flow_nominal={10,10,10}, dp_nominal={10,10,10},\r\n  " +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += "Buildings.Fluid.Movers.FlowControlled_m_flow pmpCoo(m_flow_nominal=10, allowFlowReversal=false, \r\n  " +
                $"redeclare package Medium={medium}, redeclare Buildings.Fluid.Movers.Data.Pumps.Wilo.VeroLine80slash115dash2comma2slash2 per,\r\n  " +
                "inputType=Buildings.Fluid.Types.InputType.Stages, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += $"Buildings.Fluid.MixingVolumes.MixingVolume vol(nPorts=2, m_flow_nominal=10, V=1, redeclare package Medium={medium}, allowFlowReversal=false);\n";
            module += $"Buildings.HeatTransfer.Sources.FixedTemperature Tcold(T=273.15 + {setPoint[1]});\n";
            // addon source collector/splitter
            module += "Buildings.Fluid.FixedResistances.Junction spl4(m_flow_nominal={10,10,10}, dp_nominal={10,10,10},\r\n  " +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl5(m_flow_nominal={10,10,10}, dp_nominal={10,10,10},\r\n  " +
                $"redeclare package Medium={medium}, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            // pump and valve controls
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse stepPump(shift={occSch[0] * 3600}, period=86400, width={Math.Round((occSch[1] - occSch[0]) / 24, 3)});\n";
            module += "Modelica.Blocks.Math.RealToInteger realToInteger();\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse stepValve(shift={occSch[0] * 3600}, period=86400, width={Math.Round((occSch[1] - occSch[0]) / 24, 3)});\n";
            module += "Modelica.Blocks.Sources.RealExpression valCooExp(y = 1 - stepValve.y);\n";

            string equation = "";
            equation += "connect(bou.ports[1], heater.port_a);\n";
            equation += "connect(heater.port_b, spl.port_1);\n";
            equation += "connect(pmpHea.port_a, spl1.port_2);\n";
            equation += "connect(pmpHea.port_b, heater.port_a);\n";
            equation += "connect(Thot.y, heater.TSet);\n";
            equation += "connect(spl1.port_3, spl.port_3);\n";
            equation += "connect(spl.port_2, valHea.port_a);\n";


            equation += "connect(spl2.port_2, valCoo.port_a);\n";
            equation += "connect(spl3.port_3, spl2.port_3);\n";
            equation += "connect(spl3.port_2, pmpCoo.port_a);\n";
            equation += "connect(vol.ports[1], pmpCoo.port_b);\n";
            equation += "connect(vol.ports[2], spl2.port_1);\n";
            equation += "connect(Tcold.port, vol.heatPort);\n";

            equation += "connect(valHea.port_b, spl4.port_1);\n";
            equation += "connect(valCoo.port_b, spl4.port_2);\n";
            equation += "connect(spl1.port_1, spl5.port_1);\n";
            equation += "connect(spl3.port_1, spl5.port_2);\n";
            // this node acts as the collector and splitter, ideally can be connected to multiple node
            sysSupply = new Port("port_3", "spl4");
            sysReturn = new Port("port_3", "spl5");
            pumpChain = new Port("y", "realToInteger");
            valveChain = new Port("y", "stepValve");

            // control logic
            equation += "connect(stepValve.y, valHea.y);\n"; // valCooExp relates to stepValve
            equation += "connect(valCooExp.y, valCoo.y);\n";
            equation += "connect(stepPump.y, realToInteger.u);\n";
            equation += "connect(realToInteger.y, pmpHea.stage);\n";
            equation += "connect(realToInteger.y, pmpCoo.stage);\n";

            return new Tuple<string, string>(module, equation);
        }

        public static Tuple<string, string> IdealSource2Pipe(double[] setPoint, double[] occSch, 
            out Port srcSupply, out Port srcReturn)
        {
            string module = "";
            string equation = "";

            module += "Buildings.Fluid.Sources.Boundary_pT source(redeclare package Medium=MediumW, nPorts=1, use_p_in=true, use_T_in=true);\n";
            module += "Buildings.Fluid.Sources.Boundary_pT sink(redeclare package Medium=MediumW, nPorts=1, use_p_in=true, use_T_in=true);\n";
            // seasonal temperature settings
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse srcTemp(amplitude={setPoint[0] - setPoint[1]}, offset=273.15 + {setPoint[1]}, " +
                $"period = 31536000, shift = -1900710, width = 0.5, y(displayUnit = \"degC\", unit = \"K\"));\n";
            module += "Modelica.Blocks.Sources.Constant srcPressure(k=300000);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl1(m_flow_nominal={10,10,10}, dp_nominal={10,10,10},\r\n  " +
                $"redeclare package Medium=MediumW, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            module += "Buildings.Fluid.FixedResistances.Junction spl2(m_flow_nominal={10,10,10}, dp_nominal={10,10,10},\r\n  " +
                $"redeclare package Medium=MediumW, energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState);\n";
            equation += "connect(source.ports[1], spl1.port_1);\n";
            equation += "connect(spl1.port_3, spl2.port_3);\n";
            equation += "connect(sink.ports[1], spl2.port_1);\n";
            equation += "connect(source.T_in, srcTemp.y);\n";
            equation += "connect(source.p_in, srcPressure.y);\n";
            equation += "connect(sink.T_in, srcTemp.y);\n";
            equation += "connect(sink.p_in, srcPressure.y);\n";

            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse srcPumpCtrl(shift={occSch[0] * 3600}, period=86400, width={Math.Round((occSch[1] - occSch[0])/24, 3)});\n";
            module += "Modelica.Blocks.Math.RealToInteger srcR2I();\n";
            equation += "connect(srcPumpCtrl.y, srcR2I.u);\n";
            srcSupply = new Port("port_2", "spl1");
            srcReturn = new Port("port_2", "spl2");

            return new Tuple<string, string>(module, equation);
        }

        public static Tuple<string, string> VAV(int num, double mCooAir_flow_nominal, double mHeaAir_flow_nominal, double mOutAir_flow_nominal,
            double[] setPoint, double[] occSch, 
            Port srcHea, Port sinHea, Port srcCoo, Port sinCoo, out Port tSetHea, out Port tSetCoo, 
            out Port ducSupply, out Port ducReturn, out Port pSetDuc, out Port TZonMin, out Port TZonAvg)
        {
            string module = "";
            string equation = "";
            module += $"parameter Boolean allowFlowReversal=true;\n";
            module += $"Modelica.Blocks.Routing.RealPassThrough TOut(y(final quantity=\"ThermodynamicTemperature\",final unit=\"K\",displayUnit=\"degC\",min=0));\n";
            module += $"Buildings.Fluid.Sources.Outside amb(redeclare package Medium = MediumA, nPorts=3);\n";
            module += $"Buildings.Controls.SetPoints.OccupancySchedule occSch(occupancy=3600*{{{occSch[0]},{occSch[1]}}});\n";
            // user defined
            module += $"parameter Modelica.Units.SI.Temperature TCooAirMix_nominal(displayUnit=\"degC\")=303.15;\n";
            module += $"parameter Modelica.Units.SI.Temperature TCooAirSup_nominal(displayUnit=\"degC\")=285.15;\n";
            module += $"parameter Modelica.Units.SI.MassFraction wCooAirMix_nominal=0.017;\n";
            module += $"parameter Modelica.Units.SI.Temperature TCooWatInl_nominal(displayUnit=\"degC\")=279.15;\n";
            module += $"parameter Modelica.Units.SI.Temperature THeaAirMix_nominal(displayUnit=\"degC\")=277.15;\n";
            module += $"parameter Modelica.Units.SI.Temperature THeaAirSup_nominal(displayUnit=\"degC\")=285.15;\n";
            module += $"parameter Modelica.Units.SI.Temperature THeaWatInl_nominal(displayUnit=\"degC\")=318.15;\n";
            // flowrate calc.
            module += $"parameter Modelica.Units.SI.MassFlowRate mAir_flow_nominal={mCooAir_flow_nominal};\n";
            module += $"parameter Modelica.Units.SI.HeatFlowRate QHeaAHU_flow_nominal(min=0)=700*{mHeaAir_flow_nominal}*(THeaAirSup_nominal-THeaAirMix_nominal);\n";
            module += $"parameter Modelica.Units.SI.HeatFlowRate QCooAHU_flow_nominal(max=0)=1.3*700*{mCooAir_flow_nominal}*(TCooAirSup_nominal-TCooAirMix_nominal);\n";
            module += $"parameter Modelica.Units.SI.MassFlowRate mHeaWat_flow_nominal=QHeaAHU_flow_nominal/4186/10;\n";
            module += $"parameter Modelica.Units.SI.MassFlowRate mCooWat_flow_nominal=QCooAHU_flow_nominal/4186/(-6);\n";
            module += $"parameter Modelica.Units.SI.VolumeFlowRate Vot_flow_nominal={mOutAir_flow_nominal}/0.75;\n";
            // AHU part
            module += $"Buildings.Fluid.Actuators.Dampers.Exponential damRet(\r\n    redeclare package Medium = MediumA, m_flow_nominal=mAir_flow_nominal,\r\n    from_dp=false, riseTime=15, dpDamper_nominal=5, dpFixed_nominal=5);\n";
            module += $"Buildings.Fluid.Actuators.Dampers.Exponential damOut(\r\n    redeclare package Medium = MediumA, m_flow_nominal=mAir_flow_nominal,\r\n    from_dp=false, riseTime=15, dpDamper_nominal=5, dpFixed_nominal=5);\n";
            module += $"Buildings.Fluid.Actuators.Dampers.Exponential damExh(from_dp=false, riseTime=15, dpFixed_nominal=5,\r\n    redeclare package Medium = MediumA, m_flow_nominal=mAir_flow_nominal, dpDamper_nominal=5);\n";
            module += $"Buildings.Fluid.HeatExchangers.DryCoilEffectivenessNTU heaCoi(\r\n    redeclare package Medium1=MediumW, redeclare package Medium2=MediumA, Q_flow_nominal=QHeaAHU_flow_nominal,\r\n    m1_flow_nominal=mHeaWat_flow_nominal, m2_flow_nominal={mHeaAir_flow_nominal}, \r\n    configuration=Buildings.Fluid.Types.HeatExchangerConfiguration.CounterFlow,\r\n    dp1_nominal=3000, dp2_nominal=0,\r\n    allowFlowReversal1=false, allowFlowReversal2=allowFlowReversal,\r\n    T_a1_nominal=THeaWatInl_nominal, T_a2_nominal=THeaAirMix_nominal);\n";
            module += $"Buildings.Fluid.HeatExchangers.WetCoilEffectivenessNTU cooCoi(\r\n    redeclare package Medium1=MediumW, redeclare package Medium2=MediumA, use_Q_flow_nominal=true,\r\n    Q_flow_nominal=QCooAHU_flow_nominal, m1_flow_nominal=mCooWat_flow_nominal, m2_flow_nominal={mCooAir_flow_nominal},\r\n    dp2_nominal=0, dp1_nominal=3000, T_a1_nominal=TCooWatInl_nominal, T_a2_nominal=TCooAirMix_nominal,\r\n    w_a2_nominal=wCooAirMix_nominal, energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial,\r\n    allowFlowReversal1=false, allowFlowReversal2=allowFlowReversal);\n";
            module += $"Buildings.Fluid.Movers.Preconfigured.SpeedControlled_y fanSup(\r\n    redeclare package Medium = MediumA, m_flow_nominal=mAir_flow_nominal,\r\n    dp_nominal=780 + 10 + 12, energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial);\n";
            // sensors
            module += $"Buildings.Fluid.Sensors.VolumeFlowRate senSupFlo(redeclare package Medium=MediumA, m_flow_nominal=mAir_flow_nominal);\n";
            module += $"Buildings.Fluid.Sensors.VolumeFlowRate senRetFlo(redeclare package Medium=MediumA, m_flow_nominal=mAir_flow_nominal);\n";
            module += $"Buildings.Fluid.Sensors.RelativePressure dpDisSupFan(redeclare package Medium=MediumA);\n";
            module += $"Buildings.Fluid.Sensors.TemperatureTwoPort TSup(\r\n    redeclare package Medium = MediumA,\r\n    m_flow_nominal=mAir_flow_nominal,\r\n    allowFlowReversal=allowFlowReversal);\n";
            module += $"Buildings.Fluid.Sensors.TemperatureTwoPort TRet(\r\n    redeclare package Medium = MediumA, m_flow_nominal=mAir_flow_nominal,\r\n    allowFlowReversal=allowFlowReversal);\n";
            module += $"Buildings.Fluid.Sensors.TemperatureTwoPort TMix(\r\n    redeclare package Medium = MediumA, m_flow_nominal=mAir_flow_nominal,\r\n    allowFlowReversal=allowFlowReversal, transferHeat=true);\n";
            module += $"Buildings.Fluid.Sensors.VolumeFlowRate VOut1(redeclare package Medium=MediumA, m_flow_nominal=mAir_flow_nominal);\n";
            ducSupply = new Port("port_b", "senSupFlo");
            ducReturn = new Port("port_a", "senRetFlo");
            module += $"Buildings.Fluid.FixedResistances.Junction splRetOut(\r\n    redeclare package Medium = MediumA,\r\n    tau=15,\r\n    m_flow_nominal=mAir_flow_nominal*{{1,1,1}},\r\n    energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial,\r\n    dp_nominal(each displayUnit=\"Pa\") = {{0,0,0}},\r\n    portFlowDirection_1=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Entering,\r\n    portFlowDirection_2=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving,\r\n    portFlowDirection_3=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Entering,\r\n    linearized=true);\n";
            // NTU to source
            module += $"Buildings.Fluid.Movers.Preconfigured.SpeedControlled_y pumCooCoi(\r\n    redeclare package Medium = MediumW, m_flow_nominal=mCooWat_flow_nominal,\r\n    dp_nominal=3000, energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial);\n";
            module += $"Buildings.Fluid.Movers.Preconfigured.SpeedControlled_y pumHeaCoi(\r\n    redeclare package Medium = MediumW, m_flow_nominal=mHeaWat_flow_nominal,\r\n    dp_nominal=3000, energyDynamics=Modelica.Fluid.Types.Dynamics.FixedInitial);\n";
            module += $"Buildings.Fluid.Actuators.Valves.TwoWayEqualPercentage valCooCoi(\r\n    redeclare package Medium = MediumW, m_flow_nominal=mCooWat_flow_nominal,\r\n    dpValve_nominal=6000, dpFixed_nominal=0);\n";
            module += $"Buildings.Fluid.Actuators.Valves.TwoWayEqualPercentage valHeaCoi(\r\n    redeclare package Medium = MediumW, m_flow_nominal=mHeaWat_flow_nominal,\r\n    dpValve_nominal=6000, dpFixed_nominal=0);\n";
            module += $"Buildings.Fluid.FixedResistances.Junction splCooSup(\r\n  redeclare package Medium = MediumW,\r\n  m_flow_nominal=mCooWat_flow_nominal*{{1,1,1}},\r\n  energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState,\r\n  dp_nominal(each displayUnit=\"Pa\") = {{0,0,0}},\r\n  portFlowDirection_1=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Entering,\r\n    portFlowDirection_2=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving,\r\n    portFlowDirection_3=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving);\n";
            module += $"Buildings.Fluid.FixedResistances.Junction splCooRet(\r\n  redeclare package Medium = MediumW,\r\n  m_flow_nominal=mCooWat_flow_nominal*{{1,1,1}},\r\n  energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState,\r\n  dp_nominal(each displayUnit=\"Pa\") = {{0,0,0}},\r\n  portFlowDirection_1=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Entering,\r\n    portFlowDirection_2=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving,\r\n    portFlowDirection_3=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving);\n";
            module += $"Buildings.Fluid.FixedResistances.Junction splHeaRet(\r\n  redeclare package Medium = MediumW,\r\n  m_flow_nominal=mHeaWat_flow_nominal*{{1,1,1}},\r\n  energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState,\r\n  dp_nominal(each displayUnit=\"Pa\") = {{0,0,0}},\r\n  portFlowDirection_1=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Entering,\r\n    portFlowDirection_2=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving,\r\n    portFlowDirection_3=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving);\n";
            module += $"Buildings.Fluid.FixedResistances.Junction splHeaSup(\r\n  redeclare package Medium = MediumW,\r\n  m_flow_nominal=mHeaWat_flow_nominal*{{1,1,1}},\r\n  energyDynamics=Modelica.Fluid.Types.Dynamics.SteadyState,\r\n  dp_nominal(each displayUnit=\"Pa\") = {{0,0,0}},\r\n  portFlowDirection_1=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Entering,\r\n    portFlowDirection_2=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving,\r\n    portFlowDirection_3=if allowFlowReversal then Modelica.Fluid.Types.PortFlowDirection.Bidirectional\r\n         else Modelica.Fluid.Types.PortFlowDirection.Leaving);\n";
            // control presets
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.FanVFD conFanSup(xSet_nominal(final unit=\"Pa\", displayUnit=\"Pa\") = 410, r_N_min=0.1);\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.ModeSelector modeSelector;\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.ControlBus controlBus;\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.Economizer conEco(have_reset=true, have_frePro=true, VOut_flow_min=Vot_flow_nominal);\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.RoomTemperatureSetpoint TSetRoo(\r\n  " +
                $"final THeaOn={273.15 + setPoint[0]}, final THeaOff=273.15, final TCooOn={273.15 + setPoint[1]}, final TCooOff=333.15);\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.DuctStaticPressureSetpoint pSetDuc(nin={num}, pMin=50);\n";
            pSetDuc = new Port("u", "pSetDuc"); // can wire in all VAVBox.y_actural of each zone
            // control utilities
            module += $"Buildings.Controls.OBC.CDL.Reals.MultiMin TZonMin(final nin={num}, u(each final unit=\"K\", each displayUnit=\"degC\"), y(final unit=\"K\", displayUnit=\"degC\"));\n";
            module += $"Buildings.Utilities.Math.Average TZonAve(final nin={num}, u(each final unit=\"K\", each displayUnit=\"degC\"), y(final unit=\"K\", displayUnit=\"degC\"));\n";
            module += $"Buildings.Controls.OBC.CDL.Logical.Or or2;\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.SupplyAirTemperature conTSup;\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.SupplyAirTemperatureSetpoint TSupSet;\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.SystemHysteresis sysHysHea;\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.SystemHysteresis sysHysCoo;\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Switch swiFreStaPum;\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Switch swiFreStaVal;\n";
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Constant yFreHeaCoi(final k=1.0);\n";
            module += $"Buildings.Examples.VAVReheat.BaseClasses.Controls.FreezeStat freSta;\n";

            equation += $"connect(fanSup.port_b, dpDisSupFan.port_a);\n";
            equation += $"connect(TSup.port_a, fanSup.port_b);\n";
            equation += $"connect(amb.ports[1], VOut1.port_a);\n";
            // building.weaBus is a fixed port of upper level
            //equation += $"connect(building.weaBus, weaBus);\n";
            equation += $"connect(weaBus.TDryBul, TOut.u);\n";
            equation += $"connect(weaBus, amb.weaBus);\n";

            //equation += $"connect(senRetFlo.port_a, dpRetDuc.port_b);\n";
            equation += $"connect(TSup.port_b, senSupFlo.port_a);\n";
            equation += $"connect(dpDisSupFan.port_b, amb.ports[2]);\n";
            equation += $"connect(senRetFlo.port_b, TRet.port_a);\n";
            equation += $"connect(TMix.port_b, heaCoi.port_a2);\n";
            equation += $"connect(heaCoi.port_b2, cooCoi.port_a2);\n";
            equation += $"connect(VOut1.port_b, damOut.port_a);\n";
            equation += $"connect(damRet.port_a, TRet.port_b);\n";
            equation += $"connect(pumHeaCoi.port_b, heaCoi.port_a1);\n";
            equation += $"connect(cooCoi.port_b1, pumCooCoi.port_a);\n";
            equation += $"connect(splCooSup.port_2, cooCoi.port_a1);\n";
            equation += $"connect(splCooRet.port_3, splCooSup.port_3);\n";
            equation += $"connect(pumCooCoi.port_b, splCooRet.port_2);\n";
            equation += $"connect(splHeaSup.port_2, pumHeaCoi.port_a);\n";
            equation += $"connect(heaCoi.port_b1, splHeaRet.port_2);\n";
            equation += $"connect(splHeaRet.port_3, splHeaSup.port_3);\n";
            equation += $"connect(splHeaSup.port_1, valHeaCoi.port_b);\n";
            equation += $"connect(splCooSup.port_1, valCooCoi.port_b);\n";
            equation += $"connect({srcHea}, valHeaCoi.port_a);\n";
            equation += $"connect({sinHea}, splHeaRet.port_1);\n";
            equation += $"connect({srcCoo}, valCooCoi.port_a);\n";
            equation += $"connect({sinCoo}, splCooRet.port_1);\n";
            equation += $"connect(cooCoi.port_b2, fanSup.port_a);\n";
            equation += $"connect(damOut.port_b, splRetOut.port_1);\n";
            equation += $"connect(splRetOut.port_2, TMix.port_a);\n";
            equation += $"connect(damRet.port_b, splRetOut.port_3);\n";

            // upper level
            equation += $"connect(controlBus, modeSelector.cb);\n";
            equation += $"connect(TZonAve.y, controlBus.TZonAve);\n";
            equation += $"connect(TRet.T, conEco.TRet);\n";
            equation += $"connect(TSetRoo.controlBus, controlBus);\n";
            equation += $"connect(dpDisSupFan.p_rel, conFanSup.u_m);\n";

            equation += $"connect(pSetDuc.y, conFanSup.u);\n";
            equation += $"connect(conEco.VOut_flow, VOut1.V_flow);\n";

            equation += $"connect(occSch.tNexOcc, controlBus.dTNexOcc);\n";
            equation += $"connect(occSch.occupied, controlBus.occupied);\n";
            equation += $"connect(TOut.y, controlBus.TOut);\n";
            equation += $"connect(controlBus, conEco.controlBus);\n";
            equation += $"connect(modeSelector.yFan, conFanSup.uFan);\n";
            equation += $"connect(conFanSup.y, fanSup.y);\n";
            equation += $"connect(or2.u2, modeSelector.yFan);\n";
            equation += $"connect(TSup.T, conTSup.TSup);\n";
            equation += $"connect(conTSup.yOA, conEco.uOATSup);\n";
            equation += $"connect(or2.y, conTSup.uEna);\n";
            equation += $"connect(modeSelector.yEco, conEco.uEna);\n";
            equation += $"connect(TMix.T, conEco.TMix);\n";
            equation += $"connect(controlBus, TSupSet.controlBus);\n";
            equation += $"connect(TSupSet.TSet, conTSup.TSupSet);\n";
            equation += $"connect(damRet.y, conEco.yRet);\n";
            equation += $"connect(damExh.y, conEco.yOA);\n";
            equation += $"connect(damOut.y, conEco.yOA);\n";
            equation += $"connect(damExh.port_a, TRet.port_b);\n";
            equation += $"connect(freSta.y, or2.u1);\n";
            equation += $"connect(conTSup.yHea, sysHysHea.u);\n";
            equation += $"connect(conTSup.yCoo, sysHysCoo.u);\n";
            equation += $"connect(sysHysCoo.y, valCooCoi.y);\n";
            equation += $"connect(sysHysCoo.yPum, pumCooCoi.y);\n";
            equation += $"connect(sysHysCoo.sysOn, modeSelector.yFan);\n";
            equation += $"connect(sysHysHea.sysOn, modeSelector.yFan);\n";
            equation += $"connect(yFreHeaCoi.y, swiFreStaPum.u1);\n";
            equation += $"connect(yFreHeaCoi.y, swiFreStaVal.u1);\n";
            equation += $"connect(freSta.y, swiFreStaPum.u2);\n";
            equation += $"connect(freSta.y, swiFreStaVal.u2);\n";
            equation += $"connect(sysHysHea.y, swiFreStaVal.u3);\n";
            equation += $"connect(sysHysHea.yPum, swiFreStaPum.u3);\n";
            equation += $"connect(swiFreStaPum.y, pumHeaCoi.y);\n";
            equation += $"connect(swiFreStaVal.y, valHeaCoi.y);\n";
            equation += $"connect(TZonMin.y, controlBus.TZonMin);\n";
            //equation += $"connect(TZonMin.u, TRoo);\n";
            //equation += $"connect(TZonAve.u, TRoo);\n";
            TZonMin = new Port("u", "TZonMin");
            TZonAvg = new Port("u", "TZonAve");
            equation += $"connect(freSta.u, TMix.T);\n";
            equation += $"connect(damExh.port_b, amb.ports[3]);\n";
            // output VAV box setpoint from control bus
            tSetHea = new Port("TRooSetHea", "controlBus");
            tSetCoo = new Port("TRooSetCoo", "controlBus");
            equation += $"\n";

            return new Tuple<string, string>(module, equation);
        }
    }

    class SerializeMO
    {
        /// <summary>
        /// framework of the Modleica scripts, into module declaration, equation, annotation, 3 parts
        /// </summary>
        public static string Framework(SimulationSettings simSets, string projPath, string modelName, Tuple<string, string> scriptTemplate)
        {
            // -------------------------- module initiation --------------------------------
            string script = "";
            script += "within ;\n\n";
            script += $"model {modelName}\n";
            script += $"extends Modelica.Icons.Example;\n";
            script += "replaceable package MediumA = Buildings.Media.Air;\n";
            script += "replaceable package MediumW = Buildings.Media.Water;\n";
            script += Preset.WeightedAverage();
            script += Preset.Entwine();
            // connect to EnergyPlus model
            string idfPath = Path.Combine(projPath, "ref.idf").Replace(@"\", @"\\");
            string epwPath = Path.Combine(projPath, "ref.epw").Replace(@"\", @"\\");
            string mosPath = Path.Combine(projPath, "ref.mos").Replace(@"\", @"\\");

            // the building module should be serialized with multiple thermal zones for that it connects them
            // with the same weather bus. for convenient, the name 'building' is a global one, used throughout the scripts
            MO.Building building = new MO.Building(idfPath, epwPath, mosPath);
            script += building.Serialize();
            script += "Buildings.BoundaryConditions.WeatherData.Bus weaBus;\n";
            // others
            script += scriptTemplate.Item1;
            // -------------------------- model equation ----------------------------------
            script += "\ninitial equation\n";
            script += "\nequation\n";
            script += "connect(building.weaBus, weaBus);\n\n";
            script += scriptTemplate.Item2;

            // -------------------------- annotations --------------------------------------
            // simulation configurations
            // wire this with another component for general settings
            Documentation newSim = new Documentation();
            newSim.Info = simSets.info;
            newSim.StartTime = simSets.startTime;
            newSim.StopTime = simSets.stopTime;
            newSim.Interval = simSets.interval;
            newSim.Tolerance = simSets.tolerance;
            newSim.Algorithm = simSets.algorithm;
            script += newSim.Serialize();

            script += $"end {modelName};";
            return script;
        }

        /// <summary>
        /// Modelica scripts only the building space part. Create thermal zone connections with EnergyPlus
        /// and initiate their connections. for now, only ducts are allowed for connection within control zones
        /// thermoType: 0 - use major room, 1 - use average room temperature, 2 - use return duct temp
        /// </summary>
        public static Tuple<string, string> SerializeControlZone(int id, ControlZone jsZone,
            out Port zonInlet, out Port zonOutlet, out Port sensor)
        {
            // items in this zone
            List<FunctionSpace> jsSpaces = jsZone.rooms;
            List<ThermalZone> zones = new List<ThermalZone>();
            List<MassFlowSource> flows = new List<MassFlowSource>();
            Boundary_pT pAtm = new Boundary_pT($"pAtm_{id}", "MediumA", jsZone.rooms.Count, true);
            Port.Connect(pAtm.T_in, new Port("TDryBul", "weaBus"));
            Dictionary<string, ThermalZone> zoneDict = new Dictionary<string, ThermalZone>();
            sensor = null;
            for (int i = 0; i < jsSpaces.Count; i++)
            {
                string spaceName = $"zon_{id}_{i}";

                ThermalZone space = new ThermalZone(spaceName, "MediumA", jsSpaces[i].name, jsSpaces[i].volume, 0);
                // internal gain should be a value defined by space function
                // or you can leave it to EnergyPlus to solve
                // array q_gain has 3 numbers as inputs

                // only apply one-direction connetion between constant/bus with component?
                space.qGai_flow.to.Add(space.qIntGai.y);
                space.ports[0] = new Port($"ports[1]", spaceName); // for mass flow
                space.ports[1] = new Port($"ports[2]", spaceName); // for p drop
                space.ports[2] = new Port($"ports[3]", spaceName); // for air loop inlet
                space.ports[3] = new Port($"ports[4]", spaceName); // for air loop outlet

                // not so good to represent the room control in this way
                // better to change the jsZone.sensor to the id of a specific room
                if (space.zoneName == jsZone.sensor)
                    sensor = space.TAir;
                // only cache the data. it will not be used

                // paired with a mass flow port
                string flowName = $"leakAir_{id}_{i}";
                // leakage 0.3*VRoo*1.2/3600 Outdoor air mass flow rate, assuming constant infiltration air flow rate [kg/s]
                double infiltration = 0.3 * jsSpaces[i].volume * 1.2 / 3600;
                MassFlowSource leakage = new MassFlowSource(flowName, "MediumA", infiltration);
                leakage.ports.Add(new Port($"ports[{leakage.ports.Count + 1}]", flowName));
                space.leak = leakage;
                // paired with a pressure drop with outdoor air
                string ductName = $"outDuc_{id}_{i}"; 
                PressureDrop crack = new PressureDrop(ductName, "MediumA", 20, infiltration, false, true, true);

                // this mutual assignment indicates a bad data structure, we'll fix it later
                Port.Connect(pAtm.ports[i], crack.port_b);
                Port.Connect(space.ports[0], leakage.ports[0]);
                space.ports[1].to.Add(crack.port_a);
                space.crack = crack;

                flows.Add(leakage);
                zones.Add(space);
                zoneDict.Add(jsSpaces[i].id, space);
            }

            // compile the network in this zone
            // in this process, the relay points are removed
            // be careful that the flowrate attribute is assigned when creating a bridging edge
            // this part should be isolated as another function()
            List<ConduitNode> jsZonNodes = jsZone.network.nodes;
            List<ConduitEdge> jsZonEdges = jsZone.network.edges;

            // this is a lenient version function
            // I will make a more adaptive one and put it in the PathFinding.cs
            SimplifyTree(jsZonNodes, jsZonEdges);

            // iterate all terminal node to find the target zone's ports
            Dictionary<string, Port> zonInlets = new Dictionary<string, Port>() { };
            Dictionary<string, Port> zonOutlets = new Dictionary<string, Port>() { };
            foreach (ConduitNode jsNode in jsZonNodes)
            {
                if (jsNode.linkedTerminalId != null)
                {
                    zonInlets.Add(jsNode.id, zoneDict[jsNode.linkedTerminalId].ports[2]);
                    zonOutlets.Add(jsNode.id, zoneDict[jsNode.linkedTerminalId].ports[3]);
                }
            }

            // add temperature sensor to the return main duct
            var TZonRet = new SensorT($"TRet_zon{id}", "MediumA", jsZone.airCooFlow);
            SerializeNetwork(jsZonNodes, jsZonEdges, zonInlets, zonOutlets, $"netZon{id}", "MediumA", false,
                out Port ducSupply, out Port ducReturn, out List<PressureDrop> zonPDrops, out List<Junction> zonTJoints);
            Port.Connect(TZonRet.port_a, ducReturn);
            zonInlet = ducSupply;
            zonOutlet = TZonRet.port_b;

            // zone temperature feedback: 1. delegate zone, 2. return duct, 3. space average
            // for convenience, consider no temperature sensor mounted at the return duct
            // by default, take the average temperatue of spaces within this control zone
            double[] volumes = new double[jsSpaces.Count];
            for (int i = 0; i < jsSpaces.Count; i++)
                volumes[i] = jsSpaces[i].volume;

            // if no room acts as thermostat, take the average temp of all rooms
            MassAverage avg = new MassAverage($"AvgTemp_{id}", zones.Count, volumes);
            for (int i = 0; i < zones.Count; i++)
                Port.Connect(avg.u[i], zones[i].TAir);
            if (sensor == null)
                if (jsZone.sensor == "TempAverage")
                    sensor = avg.y;
                else if (jsZone.sensor == "TempReturnDuct")
                    sensor = TZonRet.T;

            // ---------------------------- Serialization part --------------------------
            string module = "";
            string equation = "";

            module += $"// Control zone {id} module...\n";
            module += pAtm.Serialize();
            foreach (ThermalZone zone in zones)
            {
                module += zone.Serialize();
                // each zone is paired with a mass flow rate port, which names after the zone
                // for example: zone1 -> freshAir1
                module += zone.leak.Serialize(); // flow source
                module += zone.qIntGai.Serialize();
                module += zone.crack.Serialize();
            }
            foreach (Junction tJoint in zonTJoints)
                module += tJoint.Serialize();
            foreach (PressureDrop pDrop in zonPDrops)
                module += pDrop.Serialize();
            module += avg.Serialize();
            module += TZonRet.Serialize();

            // equation -------------------------------------

            // you may use a list recording all ports connected
            // thermal zone session
            equation += $"// Control zone {id} connections...\n";
            equation += pAtm.T_in.Serialize();
            // 为了统一管线序列化函数，参与管线的各节点是完全连接，这里房间不再与管线重复连接
            // 所以仅连接zone.ports[1] zone.ports[2]两个自治节点
            // 而事实上，重复连接并不会影响模拟，仅为脚本整洁考虑
            foreach (ThermalZone zone in zones)
            {
                equation += $"connect({zone.qGai_flow.Name}, {zone.qGai_flow.to[0].Name});\n";
                equation += $"connect({zone.ports[0].Name}, {zone.ports[0].to[0].Name});\n";
                equation += $"connect({zone.ports[1].Name}, {zone.ports[1].to[0].Name});\n";
                // mandate the weather bus
                equation += $"connect({zone.leak.weaBus.Name}, weaBus);\n";
                equation += $"connect({zone.crack.port_b.Name}, {zone.crack.port_b.to[0].Name});\n";
            }
            // set fluid network connection
            foreach (PressureDrop pDrop in zonPDrops)
            {
                equation += $"connect({pDrop.port_a.Name}, {pDrop.port_a.to[0].Name});\n";
                equation += $"connect({pDrop.port_b.Name}, {pDrop.port_b.to[0].Name});\n";
            }
            // if not using pDrop for connection, iterate port_2 and port_3 of T-joint
            foreach (Junction tJoint in zonTJoints)
            {
                equation += $"connect({tJoint.port_2.Name}, {tJoint.port_2.to[0].Name});\n";
                equation += $"connect({tJoint.port_3.Name}, {tJoint.port_3.to[0].Name});\n";
            }
            // inlet/outlet of this network
            equation += TZonRet.port_a.Serialize();
            // sensor output
            foreach (Port monitor in avg.u)
                equation += $"connect({monitor.Name}, {monitor.to[0].Name});\n";
            // if else, the control logic connects the sensor.Port directly
            // for example, connect(?, zone_10_1.TAir)

            return new Tuple<string, string>(module, equation);
        }

        // previous version backup in obsidian
        public static Tuple<string, string> TemplateFanCoil(Floorplan jsFloorplan)
        {
            // follow a top-down sequence: source -> shaft -> system -> zone -> space
            string module = "";
            string equation = "";

            bool isHeatingMode = false;
            double[] occSch = jsFloorplan.systems[0].schedule;

            // global setting
            // srcSupply -> water splitter, connected with 3-valve of each sub-system
            // srcReturn -> water collector, connected iwth 3-valve of each sub-system
            var srcZip = Preset.IdealSource2Pipe(new double[] { jsFloorplan.systems[0].hwTempSupply, jsFloorplan.systems[0].chwTempSupply },
                occSch, out Port srcSupply, out Port srcReturn);
            // presume all system pumps are synchronized by step control
            module += $"Buildings.Controls.OBC.CDL.Reals.Sources.Pulse sysPmpChain(shift={occSch[0] * 3600}, " +
                $"period=86400, width={Math.Round((occSch[1] - occSch[0]) / 24, 3)});\n";
            module += "Modelica.Blocks.Math.RealToInteger sysR2I;\n";
            equation += "connect(sysPmpChain.y, sysR2I.u);\n";
            var sysPmpChainSig = new Port("y", "sysR2I");
            module += srcZip.Item1;
            equation += srcZip.Item2;

            // each sub_system occupies a vertical shaft, with a pump, a valve, a pressure drop
            // the valve/pump control of all shafts and the source is synchronized
            int s = 0;
            int z = 0;
            // serialize right after you compile module in each iteration
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                // each system zone in 2 parts:
                // 1. power module connecting the source and the system network
                // 2. serialized system network from power part to target zones

                Dictionary<string, Tuple<Port, Port>> sysDict = new Dictionary<string, Tuple<Port, Port>>();
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    // how to switch conditions? one exchanger for two operations?
                    // seems not possible to switch the mode of NTU coil inside model scripts
                    // here use cooling mode to size the equipment capacity
                    var hex = new Exchangers.CoilEffectivenessNTU(
                        isHeatingMode ? 0 : 1, 
                        $"hex_{z}", "MediumW", "MediumA",
                        jsZone.wFlow, jsZone.airCooFlow, 6000, 200, 
                        isHeatingMode ? jsZone.heatLoad : -jsZone.coolLoad,
                        isHeatingMode ? jsSystem.hwTempSupply+273.15 : jsSystem.chwTempSupply+273.15,
                        isHeatingMode ? jsSystem.heatSet+273.15 : jsSystem.coolSet+273.15, 0.017, 
                        false, false);
                    var fan = new MoverFlowControlled($"fan_{z}", "MediumA", 0, jsZone.airCooFlow, 200);
                    var val = new TwoWayValve($"valZon_{z}", "TwoWayLinear", "MediumW", jsZone.wFlow, 100, 8e4);
                    Port.Connect(hex.port_b2, fan.port_a);
                    Port.Connect(val.port_b, hex.port_a1);
                    // hex.port_a2 -> zone.duct.outlet
                    // fan.port_b -> zone.duct.inlet
                    // val.port_b -> equip.outlet
                    // hex.port_a1 -> equip.inlet
                    sysDict.Add(jsZone.id, new Tuple<Port, Port>(val.port_a, hex.port_b1));

                    module += fan.Serialize();
                    module += hex.Serialize();
                    module += val.Serialize();
                    equation += hex.port_b2.Serialize(); // to fan.port_a
                    equation += hex.port_a1.Serialize(); // from val.port_b
                    var zoneZip = SerializeControlZone(z, jsZone, out Port zonInlet, out Port zonOutlet, out Port sensor);
                    Port.Connect(fan.port_b, zonInlet);
                    Port.Connect(hex.port_a2, zonOutlet);
                    module += zoneZip.Item1;
                    equation += zoneZip.Item2;
                    equation += fan.port_b.Serialize();
                    equation += hex.port_a2.Serialize();
                    var ctrlZip = Preset.FanCoilControl(z, isHeatingMode, jsSystem.schedule, new double[2] {jsSystem.heatSet, jsSystem.coolSet}, 
                        sensor, fan.m_flow_in, val.y, jsZone.airCooFlow);
                    module += ctrlZip.Item1;
                    equation += ctrlZip.Item2;

                    z++;
                }

                List<ConduitNode> jsSysNodes = jsSystem.network.nodes;
                List<ConduitEdge> jsSysEdges = jsSystem.network.edges;
                SimplifyTree(jsSysNodes, jsSysEdges);
                // iterate all terminal node to find the target zone's ports
                Dictionary<string, Port> sysInlets = new Dictionary<string, Port>() { };
                Dictionary<string, Port> sysOutlets = new Dictionary<string, Port>() { };
                foreach (ConduitNode jsNode in jsSysNodes)
                {
                    if (jsNode.linkedTerminalId != null)
                    {
                        sysInlets.Add(jsNode.id, sysDict[jsNode.linkedTerminalId].Item1);
                        sysOutlets.Add(jsNode.id, sysDict[jsNode.linkedTerminalId].Item2);
                    }
                }

                // in this test case, each shaft handles one system zone
                // the energy consumption of this pump is the system zone's distribution cost
                var sysPump = new MoverFlowControlled($"pmpSys_{s}", "MediumW", 1, jsSystem.chwFlow, 4.5 * 9810); // waterhead
                // SerializeNetwork() refreshes the network connection, so you have to add the source connection
                // to the sysValve afterward. this is bad. need to update later
                SerializeNetwork(jsSysNodes, jsSysEdges, sysInlets, sysOutlets, $"netSys{s}", "MediumW", false,
                    out Port pipSupply, out Port pipReturn, out List<PressureDrop> sysPDrops, out List<Junction> sysTJoints);
                Port.Connect(srcReturn, pipReturn);
                Port.Connect(sysPump.port_b, pipSupply);
                module += $"// System zone {s} module...\n"; //
                module += sysPump.Serialize();
                Port.Connect(sysPump.stage, sysPmpChainSig);
                Port.Connect(sysPump.port_a, srcSupply);
                equation += $"// System zone {s} connections...\n"; //
                equation += sysPump.port_b.Serialize();
                equation += sysPump.stage.Serialize();
                equation += sysPump.port_a.Serialize();
                equation += srcReturn.Serialize();
                foreach (PressureDrop pDrop in sysPDrops)
                {
                    module += pDrop.Serialize();
                    equation += pDrop.port_a.Serialize();
                    equation += pDrop.port_b.Serialize();
                }
                foreach (Junction tJoint in sysTJoints)
                {
                    module += tJoint.Serialize();
                    equation += tJoint.port_2.Serialize();
                    equation += tJoint.port_3.Serialize();
                }
                
                s++;
            }

            return new Tuple<string, string> ( module, equation );
        }

        public static Tuple<string, string> TemplateIdealLoad(Floorplan jsFloorplan)
        {
            string module = "";
            string equation = "";
            int z = 0;
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    Boundary_pT pAtm = new Boundary_pT($"pAtm_{z}", "MediumA", jsZone.rooms.Count, true);
                    Port.Connect(pAtm.T_in, new Port("TDryBul", "weaBus"));
                    var jsSpaces = jsZone.rooms;
                    List<ThermalZone> spaces = new List<ThermalZone>();
                    List<double> heatLoads = new List<double>();
                    List<double> coolLoads = new List<double>();
                    for (int i = 0; i < jsSpaces.Count; i++)
                    {
                        string spaceName = $"zon_{z}_{i}";
                        heatLoads.Add(jsSpaces[i].heatLoad);
                        coolLoads.Add(jsSpaces[i].coolLoad);

                        ThermalZone space = new ThermalZone(spaceName, "MediumA", jsSpaces[i].name, jsSpaces[i].volume, 0);
                        // only apply one-direction connetion between constant/bus with component?
                        space.qGai_flow.to.Add(space.qIntGai.y);
                        space.ports[0] = new Port($"ports[1]", spaceName); // for mass flow
                        space.ports[1] = new Port($"ports[2]", spaceName); // for p drop
                        // this can be the inherit attribute of a space, the flow rate should be designated while system sizing

                        // paired with a mass flow port
                        string flowName = $"freshAir_{z}_{i}";
                        MassFlowSource outAir = new MassFlowSource(flowName, "MediumA", jsSpaces[i].airOutFlow);
                        outAir.ports.Add(new Port($"ports[{outAir.ports.Count + 1}]", flowName));
                        space.leak = outAir;
                        // paired with a pressure drop with outdoor air
                        string ductName = $"outDuc_{z}_{i}";
                        PressureDrop crack = new PressureDrop(ductName, "MediumA", 20, jsSpaces[i].airOutFlow,
                            false, true, true);                 ///////////////////////////////////////-CHECK-///////
                        // this mutual assignment indicates a bad data structure, we'll fix it later
                        Port.Connect(pAtm.ports[i], crack.port_b);
                        Port.Connect(space.ports[0], outAir.ports[0]);
                        space.ports[1].to.Add(crack.port_a);
                        space.crack = crack;
                        spaces.Add(space);
                    }

                    double[] volumes = new double[jsSpaces.Count];
                    for (int i = 0; i < jsSpaces.Count; i++)
                        volumes[i] = jsSpaces[i].volume;

                    // if no room acts as thermostat, take the average temp of all rooms
                    MassAverage avg = new MassAverage($"AvgTemp_{z}", spaces.Count, volumes);
                    for (int i = 0; i < spaces.Count; i++)
                        Port.Connect(avg.u[i], spaces[i].TAir);
                    Port sensor = avg.y;

                    // ---------------------------- Serialization part --------------------------

                    module += $"// Control zone {z} module...\n";
                    module += pAtm.Serialize();
                    foreach (ThermalZone space in spaces)
                    {
                        module += space.Serialize();
                        module += space.leak.Serialize(); // flow source
                        module += space.qIntGai.Serialize();
                        module += space.crack.Serialize();
                    }
                    module += avg.Serialize();

                    // equation -------------------------------------

                    List<Port> heatPorts = new List<Port>() { };
                    equation += $"// Control zone {z} connections...\n";
                    equation += pAtm.T_in.Serialize();
                    foreach (ThermalZone space in spaces)
                    {
                        heatPorts.Add(new Port("heaPorAir", $"{space.Name}"));
                        equation += $"connect({space.qGai_flow.Name}, {space.qGai_flow.to[0].Name});\n";
                        equation += $"connect({space.ports[0].Name}, {space.ports[0].to[0].Name});\n";
                        equation += $"connect({space.ports[1].Name}, {space.ports[1].to[0].Name});\n";
                        equation += $"connect({space.leak.weaBus.Name}, weaBus);\n";
                        equation += $"connect({space.crack.port_b.Name}, {space.crack.port_b.to[0].Name});\n";
                    }
                    
                    foreach (Port monitor in avg.u)
                        equation += $"connect({monitor.Name}, {monitor.to[0].Name});\n";

                    var scriptZip = Preset.IdealLoad($"{z}", 
                        new double[] { jsFloorplan.systems[0].heatSet, jsFloorplan.systems[0].coolSet }, 
                        jsFloorplan.systems[0].schedule, 
                        heatLoads.ToArray(), coolLoads.ToArray(), sensor, heatPorts.ToArray());
                    module += scriptZip.Item1;
                    equation += scriptZip.Item2;

                    z++;
                }
            }
            return new Tuple<string, string>(module, equation);
        }

        public static Tuple<string, string> TemplateVAVReheat(Floorplan jsFloorplan)
        {
            string module = "";
            string equation = "";

            // source settings
            int srcHeaPortCount = jsFloorplan.systems.Count;
            foreach (var jsSystem in jsFloorplan.systems)
                srcHeaPortCount += jsSystem.zones.Count;
            // 6000 is the pressure drop of the main pipe valve, presumption...
            var heatSource = new Boundary_pT($"srcHea", "MediumW", srcHeaPortCount, false, false, jsFloorplan.systems[0].hwTempSupply + 273.15, 300000 + 6000);
            var heatSink = new Boundary_pT($"sinHea", "MediumW", srcHeaPortCount, false, false, jsFloorplan.systems[0].hwTempSupply + 273.15, 300000);
            var coolSource = new Boundary_pT($"srcCoo", "MediumW", jsFloorplan.systems.Count, false, false, jsFloorplan.systems[0].chwTempSupply + 273.15, 300000 + 6000);
            var coolSink = new Boundary_pT($"sinCoo", "MediumW", jsFloorplan.systems.Count, false, false, jsFloorplan.systems[0].chwTempSupply + 273.15, 300000);
            module += heatSource.Serialize();
            module += heatSink.Serialize();
            module += coolSource.Serialize();
            module += coolSink.Serialize();

            int s = 0;
            int z = 0;
            foreach (SystemZone jsSystem in jsFloorplan.systems) // only considering one system zone for now
            {
                // into the zone level
                Dictionary<string, Tuple<Port, Port>> sysDict = new Dictionary<string, Tuple<Port, Port>>();
                double mCooAir_flow_nominal = jsSystem.airCooFlow;
                double mHeaAir_flow_nominal = jsSystem.airHeaFlow;
                double mOutAir_flow_nominal = jsSystem.airOutFlow;
                // heat source ports = [sys0, sys1, ..., sysN | zon0_sys0, zon1_sys0 | zon0_sys1 ... ]
                var sysMain = Preset.VAV(jsSystem.zones.Count, mCooAir_flow_nominal, mHeaAir_flow_nominal, mOutAir_flow_nominal,
                    new double[2] {jsSystem.heatSet, jsSystem.coolSet}, jsSystem.schedule, 
                    heatSource.ports[s], heatSink.ports[s], coolSource.ports[s], coolSink.ports[s],
                    out Port tSetHea, out Port tSetCoo, out Port ahuSupply, out Port ahuReturn, 
                    out Port pSetDuc, out Port TZonMin, out Port TZonAvg);
                module += $"Entwine TZonBus(n={jsSystem.zones.Count});\n"; // collect all zone temperatures by array
                module += $"Entwine PZonBus(n={jsSystem.zones.Count});\n"; // collect all zone acturator movements
                equation += $"connect(TZonBus.y, {TZonMin.Name});\n";
                equation += $"connect(TZonBus.y, {TZonAvg.Name});\n";
                equation += $"connect(PZonBus.y, {pSetDuc.Name});\n";
                module += "// system settings\n";
                module += sysMain.Item1;
                equation += sysMain.Item2;

                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    // VAV box for each zone
                    var vav = new Examples.VAVReheatBox($"VAVBox{z}", jsZone.rooms.Sum(x => x.volume), jsZone.airCooFlow, jsZone.airHeaFlow, false,
                        jsSystem.hwTempSupply, jsSystem.hwTempSupply - jsSystem.hwTempDelta, 12, 28);
                    var con = new Examples.RoomVAV($"conVAV{z}", false, jsZone.airCooFlow, jsZone.airHeaFlow, jsZone.airOutFlow);
                    // sensor method determines the temperature measurement of the zone
                    var zoneZip = SerializeControlZone(z, jsZone, out Port zonInlet, out Port zonOutlet, out Port roomTemp);
                    sysDict.Add(jsZone.id, new Tuple<Port, Port>(vav.port_aAir, zonOutlet));
                    Port.Connect(vav.port_bAir, zonInlet);
                    module += zoneZip.Item1;
                    equation += zoneZip.Item2;
                    equation += vav.port_bAir.Serialize();
                    // collect temp/actuator data of each zone
                    equation += $"connect({roomTemp.Name}, TZonBus.u[{z+1}]);\n";
                    
                    Port.Connect(con.TRoo, roomTemp);
                    Port.Connect(con.TRooCooSet, tSetCoo);
                    Port.Connect(con.TRooHeaSet, tSetHea);
                    Port.Connect(con.yDam, vav.yVAV);
                    Port.Connect(con.yVal, vav.yHea);
                    Port.Connect(con.VDis_flow, vav.VSup_flow);
                    equation += $"connect({vav.y_actual.Name}, PZonBus.u[{z+1}]);\n";
                    // heat source ports = [sys0, sys1, ..., sysN | zon0_sys0, zon1_sys0 | zon0_sys1 ... ]
                    Port.Connect(vav.port_bHeaWat, heatSink.ports[jsFloorplan.systems.Count + z]);
                    Port.Connect(vav.port_aHeaWat, heatSource.ports[jsFloorplan.systems.Count + z]);

                    module += vav.Serialize();
                    module += con.Serialize();
                    // not safe to batch all port serialization with the module
                    equation += con.TRoo.Serialize();
                    equation += con.TRooCooSet.Serialize();
                    equation += con.TRooHeaSet.Serialize();
                    equation += con.yDam.Serialize();
                    equation += con.yVal.Serialize();
                    equation += con.VDis_flow.Serialize();
                    equation += vav.port_bHeaWat.Serialize();
                    equation += vav.port_aHeaWat.Serialize();

                    z++;
                }

                // networking with all zones
                List<ConduitNode> jsSysNodes = jsSystem.network.nodes;
                List<ConduitEdge> jsSysEdges = jsSystem.network.edges;
                SimplifyTree(jsSysNodes, jsSysEdges);
                // iterate all terminal node to find the target zone's ports
                Dictionary<string, Port> sysInlets = new Dictionary<string, Port>() { };
                Dictionary<string, Port> sysOutlets = new Dictionary<string, Port>() { };
                foreach (ConduitNode jsNode in jsSysNodes)
                {
                    if (jsNode.linkedTerminalId != null)
                    {
                        sysInlets.Add(jsNode.id, sysDict[jsNode.linkedTerminalId].Item1);
                        sysOutlets.Add(jsNode.id, sysDict[jsNode.linkedTerminalId].Item2);
                    }
                }

                SerializeNetwork(jsSysNodes, jsSysEdges, sysInlets, sysOutlets, $"netSys{s}", "MediumA", false, 
                    out Port ducSupply, out Port ducReturn, out List<PressureDrop> sysPDrops, out List<Junction> sysTJoints);
                // connect system main duct network to AHU unit
                Port.Connect(ahuSupply, ducSupply);
                Port.Connect(ahuReturn, ducReturn);
                equation += ahuSupply.Serialize();
                equation += ahuReturn.Serialize();

                module += $"// System ducting {s} modules...\n";
                foreach (Junction tJoint in sysTJoints)
                    module += tJoint.Serialize();
                foreach (PressureDrop pDrop in sysPDrops)
                    module += pDrop.Serialize();

                // equation -------------------------------------

                equation += $"// System ducting {s} connections...\n";
                // set fluid network connection
                foreach (PressureDrop pDrop in sysPDrops)
                {
                    equation += $"connect({pDrop.port_a.Name}, {pDrop.port_a.to[0].Name});\n";
                    equation += $"connect({pDrop.port_b.Name}, {pDrop.port_b.to[0].Name});\n";
                }
                // if not using pDrop for connection, iterate port_2 and port_3 of T-joint
                foreach (Junction tJoint in sysTJoints)
                {
                    equation += $"connect({tJoint.port_2.Name}, {tJoint.port_2.to[0].Name});\n";
                    equation += $"connect({tJoint.port_3.Name}, {tJoint.port_3.to[0].Name});\n";
                }
            }

            return new Tuple<string, string>(module, equation);
        }
        

        // UTILITIES
        public static void SimplifyTree(List<ConduitNode> jsNodes, List<ConduitEdge> jsEdges)
        {
            Dictionary<string, ConduitNode> jsNodeDict = new Dictionary<string, ConduitNode>() { };
            foreach (ConduitNode jsNode in jsNodes)
            {
                jsNodeDict.Add(jsNode.id, jsNode);
            }

            bool flag = true;
            int inf_counter = 0;
            while (flag || inf_counter > 30)
            {
                inf_counter += 1;
                int node_counter = 0;
                // do not remove node with degree = 2
                // just add new edges that bridge them over
                foreach (ConduitNode jsNode in jsNodes)
                {
                    if (jsNode.degree == 1) // in a tree, the source node and relay node all have degree 1
                    {
                        if (jsNode.type == nodeTypeEnum.source)
                            continue;
                        node_counter += 1;
                        string start_id = "";
                        string end_id = "";
                        double weight = 0;
                        double friction = 0;
                        for (int j = jsEdges.Count - 1; j >= 0; j--)
                        {
                            if (jsEdges[j].startId == jsNode.id)
                            {
                                end_id = jsEdges[j].endId;
                                weight += jsEdges[j].length;
                                friction += jsEdges[j].friction;
                                jsEdges.RemoveAt(j);
                                jsNode.degree -= 1;
                            }
                            else if (jsEdges[j].endId == jsNode.id)
                            {
                                start_id = jsEdges[j].startId;
                                weight += jsEdges[j].length;
                                friction += jsEdges[j].friction;
                                jsEdges.RemoveAt(j);
                                jsNode.degree -= 1;
                            }
                        }
                        weight += 0.023;
                        if (start_id != "" && end_id != "")
                            jsEdges.Add(new ConduitEdge
                            {
                                startId = start_id,
                                endId = end_id,
                                length = weight,
                                massFlow = jsNodeDict[end_id].massFlow,
                                friction = friction
                            });
                        break;
                    }
                }
                if (node_counter == 0)
                    flag = false;
            }
        }

        public static void SerializeNetwork(List<ConduitNode> nodes, List<ConduitEdge> edges, 
            Dictionary<string, Port> terminalOutletDict, Dictionary<string, Port> terminalInletDict, 
            string prefix, string medium, bool isIdealFlow,
            out Port inlet, out Port outlet, out List<PressureDrop> pDrops, out List<Junction> tJoints)
        {
            Dictionary<string, ConduitNode> jsNodeDict = new Dictionary<string, ConduitNode>();
            foreach (ConduitNode node in nodes)
            {
                jsNodeDict.Add(node.id, node);
            }

            Port temp_inlet = new Port("foo", "temp_inlet");
            Port temp_outlet = new Port("foo", "temp_outlet");

            // do we have to recreate another graph, or we can just create T-joint and pressure drops directly?
            Dictionary<string, Junction> jointDict = new Dictionary<string, Junction>() { };
            Dictionary<string, Junction> jointDict_rev = new Dictionary<string, Junction>() { };
            pDrops = new List<PressureDrop>() { };
            tJoints = new List<Junction>() { };
            foreach (ConduitNode jsNode in nodes)
            {
                if (jsNode.degree == 2)
                {
                    // predefined pressure drop, nominal flow and res array, can be fixed later
                    // note that "Medium" is a default setting. You need to replace it when water/air are introduced
                    jointDict.Add(jsNode.id, new Junction(
                        $"{prefix}_TI{jointDict.Count}", medium, true, true, false, 
                        10, 0, new double[] { 0, 1, 1 }, new double[] { 0, 0, 0 }));
                    tJoints.Add(jointDict[jsNode.id]);
                    jointDict_rev.Add(jsNode.id, new Junction(
                        $"{prefix}_TO{jointDict_rev.Count}", medium, true, true, false, 
                        10, 0, new double[] { 0, 1, 1 }, new double[] { 0, 0, 0 }));
                    tJoints.Add(jointDict_rev[jsNode.id]);
                }
            }
            // it is a tree, you cannot guarantee that each edge follows the flow direction
            // ventilating direction path 
            foreach (ConduitEdge jsEdge in edges)
            {
                PressureDrop pDrop = new PressureDrop(
                    $"{prefix}_PI{edges.IndexOf(jsEdge)}", medium, jsEdge.friction, jsEdge.massFlow, false, true, true);

                // what is the start point? to source root or to any of the out port of T-joint
                if (jsNodeDict[jsEdge.startId].type == nodeTypeEnum.source)
                {
                    Port.Connect(pDrop.port_a, temp_inlet);
                    // leave the res blank for the sum flowrate
                }
                else
                {
                    Junction inputJunction = jointDict[jsEdge.startId]; // get the inward flow port
                    if (inputJunction.outPort == 0)
                    {
                        // connect to the 1st out port
                        Port.Connect(pDrop.port_a, inputJunction.port_2);
                        // round to the top by granularity 0.05
                        // negative value means the fuild is leaving the component
                        inputJunction.flows[1] = -jsEdge.massFlow;
                        inputJunction.outPort += 1;
                    }
                    else if (inputJunction.outPort == 1)
                    {
                        // connect to the 2nd out port
                        Port.Connect(pDrop.port_a, inputJunction.port_3);
                        // round to the top by granularity 0.05
                        // negative value means the fuild is leaving the component
                        inputJunction.flows[2] = -jsEdge.massFlow;
                        inputJunction.outPort += 1;
                    }
                    else
                        Debug.Print("Port not enough for a T-joint");
                }

                // what is the end point? to room or to T-joint
                if (jsNodeDict[jsEdge.endId].type == nodeTypeEnum.terminal)
                {
                    //Port.Connect(pDrop.port_b, terminals[jsNodeDict[jsEdge.endId].linkedTerminalId].ports[2]);
                    Port.Connect(pDrop.port_b, terminalOutletDict[jsNodeDict[jsEdge.endId].id]);
                }
                else
                {
                    // there is no stream merge in this ducting work
                    Port.Connect(pDrop.port_b, jointDict[jsEdge.endId].port_1);
                    // leave the res blank for the sum flowrate
                }
                pDrops.Add(pDrop);
            }
            // return loop?
            foreach (ConduitEdge jsEdge in edges)
            {
                PressureDrop pDrop = new PressureDrop(
                    $"{prefix}_PO{edges.IndexOf(jsEdge)}", medium, jsEdge.friction, jsEdge.massFlow, false, true, true);
                // note that the start and end points of the edge remains the same
                // you need to reverse them when connecting the pipe/duct
                // either TO from the source or TO the T-joint (merging flow)
                if (jsNodeDict[jsEdge.startId].type == nodeTypeEnum.source)
                {
                    Port.Connect(pDrop.port_b, temp_outlet);
                }
                else
                {
                    Junction outputJunction = jointDict_rev[jsEdge.startId]; // get the inward flow port
                    if (outputJunction.outPort == 0)
                    {
                        // connect to the 1st out port
                        Port.Connect(pDrop.port_b, outputJunction.port_2);
                        // round to the top by granularity 0.05
                        outputJunction.flows[1] = jsEdge.massFlow;
                        outputJunction.outPort += 1;
                    }
                    else if (outputJunction.outPort == 1)
                    {
                        // connect to the 2nd out port
                        Port.Connect(pDrop.port_b, outputJunction.port_3);
                        // round to the top by granularity 0.05
                        outputJunction.flows[2] = jsEdge.massFlow;
                        outputJunction.outPort += 1;
                    }
                    else
                        Debug.Print("Port not enough for a T-joint");
                }
                // either starting from the terminal or the merging port of a junction
                if (jsNodeDict[jsEdge.endId].type == nodeTypeEnum.terminal)
                {
                    //Port.Connect(zoneDict[jsNodeDict[jsEdge.endId].linkedTerminalId].ports[3], pDrop.port_a);
                    Port.Connect(terminalInletDict[jsNodeDict[jsEdge.endId].id], pDrop.port_a);
                }
                else
                {
                    Port.Connect(pDrop.port_a, jointDict_rev[jsEdge.endId].port_1);
                }
                pDrops.Add(pDrop);
            }

            // for all T-joints, fulfil their res value
            // take the port_1, e.g. the res[0] as the main port, [1], [2] are the branches
            foreach (Junction tJoint in tJoints)
            {
                tJoint.flows[0] = -(tJoint.flows[1] + tJoint.flows[2]);
                //tJoint.m_flow_nominal = Math.Abs(tJoint.res[1] + tJoint.res[2]);
                tJoint.m_flow_nominal = 1;
            }

            // 20231013 ------------------------------------------------------------------------------------
            // what if we remove all presure drops, only keep T-joints?
            // this is a temporary test because the pressure drops lead to fatal error in Modelica somehow
            Dictionary<string, Junction> jointNameDict = new Dictionary<string, Junction>() { };
            foreach (Junction tj in tJoints)
            {
                jointNameDict.Add(tj.Name, tj);
            }
            if (!isIdealFlow)
            {
                foreach (PressureDrop pDrop in pDrops)
                {
                    Port port_1 = pDrop.port_a.to[0];
                    Port port_2 = pDrop.port_b.to[0];
                    Port.Reconnect(port_1, port_2);

                    if (tJoints.Count > 0)
                    {
                        if (port_1.parent.Contains("TI"))
                        {
                            var tags = port_1.Name.Split('_');
                            if (tags.Last() == "2")
                                jointNameDict[port_1.parent].res[1] = -pDrop.dp_nominal;
                            if (tags.Last() == "3")
                                jointNameDict[port_1.parent].res[2] = -pDrop.dp_nominal;
                        }
                        else if (port_2.parent.Contains("TI"))
                            jointNameDict[port_2.parent].res[0] = pDrop.dp_nominal;
                        if (port_2.parent.Contains("TO"))
                        {
                            var tags = port_2.Name.Split('_');
                            if (tags.Last() == "2")
                                jointNameDict[port_2.parent].res[1] = pDrop.dp_nominal;
                            if (tags.Last() == "3")
                                jointNameDict[port_2.parent].res[2] = pDrop.dp_nominal;
                        }
                        else if (port_1.parent.Contains("TO"))
                            jointNameDict[port_1.parent].res[0] = -pDrop.dp_nominal;
                    }
                }
                pDrops = new List<PressureDrop>() { }; // empty the pDrop list
            }
            // expose the temporary inlet/outlet
            // after simplification, they have t-joints port connected
            inlet = temp_inlet.to[0];
            outlet = temp_outlet.to[0];

            return;
        }
    }
}
