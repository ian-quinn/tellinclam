import os
from OMPython import OMCSessionZMQ

os.chdir("C:/users/ianqu/desktop/clams/systest/openmodelica")
omc = OMCSessionZMQ()
omc.sendExpression("loadModel(Modelica)")
omc.sendExpression("loadFile(\"C:/Users/ianqu/Documents/Buildings/package.mo\")")
omc.sendExpression("loadFile(\"../systest.mo\")")
result = omc.sendExpression("simulate(systest)")
print(result['resultFile'] + '@', end='')
print(result['messages'] + '@', end='')
print(result['timeTotal'], end='')
