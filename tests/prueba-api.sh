#!/usr/bin/env bash
# Prueba de punta a punta contra una API ya levantada (local o en Azure).
#   Uso:  ADMIN_EMAIL=... ADMIN_PASSWORD=... ./tests/prueba-api.sh [URL_BASE]
# Cubre el camino feliz y los casos que DEBEN fallar (401, 403, 404, 409, 400).
# Deja datos con prefijo "prueba-" en la base; la API no borra clientes con reservas a propósito.

BASE="${1:-http://localhost:5165}"
ADMIN_EMAIL="${ADMIN_EMAIL:?Definí ADMIN_EMAIL}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:?Definí ADMIN_PASSWORD}"
SUFIJO="$(date +%s)"
OK=0; FALLO=0

# -X utf8: Python en Windows lee stdin con cp1252 y rompería los acentos.
# Los cuerpos JSON usan escapes \uXXXX para los acentos: curl en Git Bash pasa los argumentos en cp1252 y rompería el UTF-8.
campo() { python -X utf8 -c "import sys,json; d=json.load(sys.stdin); print(d$1)"; }

# esperar <descripcion> <codigo esperado> <metodo> <ruta> [token] [cuerpo json]
esperar() {
  local desc="$1" esperado="$2" metodo="$3" ruta="$4" token="$5" cuerpo="$6"
  local args=(-s -o /tmp/respuesta.json -w "%{http_code}" -X "$metodo" "$BASE$ruta" -H "Content-Type: application/json")
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$cuerpo" ] && args+=(-d "$cuerpo")
  local real; real=$(curl "${args[@]}")
  if [ "$real" = "$esperado" ]; then OK=$((OK+1)); echo "  ok    $desc ($real)"
  else FALLO=$((FALLO+1)); echo "  FALLO $desc: esperaba $esperado y fue $real -> $(head -c 200 /tmp/respuesta.json)"; fi
}

fecha_futura() { python -X utf8 -c "import datetime; print(datetime.date.today()+datetime.timedelta(days=$1))"; }
DIA1=$(fecha_futura 3)

echo "== Autenticación"
esperar "sin token se rechaza" 401 GET /api/clientes
esperar "login con clave incorrecta" 401 POST /api/auth/login "" '{"email":"'"$ADMIN_EMAIL"'","password":"incorrecta"}'
esperar "login de administrador" 200 POST /api/auth/login "" '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}'
TOKEN=$(campo "['token']" < /tmp/respuesta.json)

esperar "alta de empleado (admin)" 204 POST /api/auth/register "$TOKEN" '{"email":"empleado'"$SUFIJO"'@canchas.test","password":"ClaveSegura123","rol":"Empleado"}'
esperar "login de empleado" 200 POST /api/auth/login "" '{"email":"empleado'"$SUFIJO"'@canchas.test","password":"ClaveSegura123"}'
TOKEN_EMP=$(campo "['token']" < /tmp/respuesta.json)
esperar "empleado no puede dar de alta usuarios" 403 POST /api/auth/register "$TOKEN_EMP" '{"email":"x@canchas.test","password":"ClaveSegura123","rol":"Administrador"}'

echo "== Canchas"
esperar "empleado puede listar canchas" 200 GET /api/canchas "$TOKEN_EMP"
esperar "empleado no puede crear canchas" 403 POST /api/canchas "$TOKEN_EMP" '{"nombre":"X","tipoDeporte":"P\u00e1del","precioPorHora":100}'
esperar "precio inválido" 400 POST /api/canchas "$TOKEN" '{"nombre":"X","tipoDeporte":"P\u00e1del","precioPorHora":0}'
esperar "crear cancha" 201 POST /api/canchas "$TOKEN" '{"nombre":"prueba-'"$SUFIJO"'","tipoDeporte":"P\u00e1del","precioPorHora":20000}'
CANCHA=$(campo "['id']" < /tmp/respuesta.json)
esperar "cancha inexistente" 404 GET /api/canchas/999999 "$TOKEN"

echo "== Clientes"
esperar "email inválido" 400 POST /api/clientes "$TOKEN_EMP" '{"nombre":"A","apellido":"B","email":"no-es-mail","telefono":"1"}'
esperar "crear cliente" 201 POST /api/clientes "$TOKEN_EMP" '{"nombre":"Ana","apellido":"P\u00e9rez","email":"prueba-'"$SUFIJO"'@canchas.test","telefono":"1155550000"}'
CLIENTE=$(campo "['id']" < /tmp/respuesta.json)
esperar "email duplicado" 409 POST /api/clientes "$TOKEN_EMP" '{"nombre":"Ana","apellido":"P\u00e9rez","email":"prueba-'"$SUFIJO"'@canchas.test","telefono":"1"}'

echo "== Reservas"
RES='{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"'$DIA1'","horaInicio":"18:00:00","horaFin":"19:30:00"}'
esperar "crear reserva" 201 POST /api/reservas "$TOKEN_EMP" "$RES"
RESERVA=$(campo "['id']" < /tmp/respuesta.json)
echo "        total=$(campo "['total']" < /tmp/respuesta.json) (esperado 30000 = 1,5 h x 20000), estado=$(campo "['estado']" < /tmp/respuesta.json)"
esperar "superposición exacta" 409 POST /api/reservas "$TOKEN_EMP" "$RES"
esperar "superposición parcial" 409 POST /api/reservas "$TOKEN_EMP" '{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"'$DIA1'","horaInicio":"19:00:00","horaFin":"20:00:00"}'
esperar "reserva contigua (19:30-20:30) sí se permite" 201 POST /api/reservas "$TOKEN_EMP" '{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"'$DIA1'","horaInicio":"19:30:00","horaFin":"20:30:00"}'
CONTIGUA=$(campo "['id']" < /tmp/respuesta.json)
esperar "fin anterior al inicio" 400 POST /api/reservas "$TOKEN_EMP" '{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"'$DIA1'","horaInicio":"20:00:00","horaFin":"19:00:00"}'
esperar "fecha pasada" 400 POST /api/reservas "$TOKEN_EMP" '{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"2020-01-01","horaInicio":"10:00:00","horaFin":"11:00:00"}'
esperar "cliente inexistente" 404 POST /api/reservas "$TOKEN_EMP" '{"clienteId":999999,"canchaId":'$CANCHA',"fecha":"'$DIA1'","horaInicio":"10:00:00","horaFin":"11:00:00"}'
esperar "agenda del día" 200 GET "/api/reservas?fecha=$DIA1&canchaId=$CANCHA" "$TOKEN_EMP"

echo "== Pagos"
esperar "seña" 201 POST /api/reservas/$RESERVA/pagos "$TOKEN_EMP" '{"monto":10000,"metodoPago":"Efectivo"}'
PAGO=$(campo "['id']" < /tmp/respuesta.json)
esperar "sobrepago (queda saldo 20000)" 409 POST /api/reservas/$RESERVA/pagos "$TOKEN_EMP" '{"monto":25000,"metodoPago":"Transferencia"}'
esperar "saldo" 201 POST /api/reservas/$RESERVA/pagos "$TOKEN_EMP" '{"monto":20000,"metodoPago":"MercadoPago"}'
esperar "consultar reserva pagada" 200 GET /api/reservas/$RESERVA "$TOKEN_EMP"
echo "        pagado=$(campo "['totalPagado']" < /tmp/respuesta.json) saldo=$(campo "['saldo']" < /tmp/respuesta.json)"
esperar "empleado no puede anular pagos" 403 DELETE /api/pagos/$PAGO "$TOKEN_EMP"

echo "== Estados"
esperar "confirmar" 200 POST /api/reservas/$RESERVA/confirmar "$TOKEN_EMP"
esperar "no se confirma dos veces" 409 POST /api/reservas/$RESERVA/confirmar "$TOKEN_EMP"
esperar "no se modifica una confirmada" 409 PUT /api/reservas/$RESERVA "$TOKEN_EMP" "$RES"
esperar "cancelar la contigua" 200 POST /api/reservas/$CONTIGUA/cancelar "$TOKEN_EMP"
esperar "no se paga una cancelada" 409 POST /api/reservas/$CONTIGUA/pagos "$TOKEN_EMP" '{"monto":1000,"metodoPago":"Efectivo"}'
esperar "la franja cancelada queda libre" 201 POST /api/reservas "$TOKEN_EMP" '{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"'$DIA1'","horaInicio":"19:30:00","horaFin":"20:30:00"}'
esperar "no se borra un cliente con reservas" 409 DELETE /api/clientes/$CLIENTE "$TOKEN"
esperar "no se borra una cancha con reservas" 409 DELETE /api/canchas/$CANCHA "$TOKEN"

echo "== Servicio de terceros (Open-Meteo)"
esperar "clima de la reserva" 200 GET /api/reservas/$RESERVA/clima "$TOKEN_EMP"
echo "        $(cat /tmp/respuesta.json)"
LEJANA=$(fecha_futura 400)
esperar "reserva lejana para probar el límite del proveedor" 201 POST /api/reservas "$TOKEN_EMP" '{"clienteId":'$CLIENTE',"canchaId":'$CANCHA',"fecha":"'$LEJANA'","horaInicio":"10:00:00","horaFin":"11:00:00"}'
LEJANA_ID=$(campo "['id']" < /tmp/respuesta.json)
esperar "clima fuera del rango pronosticable" 409 GET /api/reservas/$LEJANA_ID/clima "$TOKEN_EMP"

echo
echo "Resultado: $OK ok, $FALLO fallos"
[ "$FALLO" -eq 0 ]
