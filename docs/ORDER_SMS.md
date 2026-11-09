# Avisos de pedidos

Tres eventos generan avisos: al crear un pedido, `OrderPlaced` se encola para el teléfono privado de la tienda; al marcarlo listo, `OrderReady` se encola para el teléfono verificado del cliente; al cancelarlo desde cocina, `OrderCancelled` se encola para ese cliente. Aceptación, preparación, entrega y rechazo no generan otros SMS. Los PIN de verificación son independientes.

En Configuración → Avisos por SMS, el encargado guarda diez dígitos mexicanos; +52 aparece separado del input. Se acepta pegar el número con prefijo. Vacío desactiva avisos a cocina. El teléfono no se devuelve en las rutas públicas del catálogo. Las tiendas existentes comienzan sin teléfono configurado; no se asume ni reutiliza un número personal.

Las tres plantillas tienen exactamente 180 caracteres con códigos de pedido de diez caracteres, sin relleno. El SMS de cancelación remite a Mis pedidos para ver el motivo completo, sin recortarlo ni incluir texto variable que altere la longitud. Se envían como JSON UTF-8 mediante Basic a SMSGate. La longitud de caracteres no equivale a bytes ni garantiza un único segmento SMS.

La cancelación solo la ejecutan encargado o cocina de la tienda, con motivo obligatorio de hasta 500 caracteres, desde Pendiente, Aceptado o EnPreparacion. Listo, Entregado, Rechazado y NoRecogido no pueden cancelarse. Un pedido cancelado no puede reactivarse; repetir su cancelación es idempotente. Queda el actor y el motivo en el historial. No se habilita cancelación por parte del cliente.

El pedido y su aviso se guardan en la misma transacción. La clave de idempotencia del pedido y el índice único (OrderId, TemplateType) evitan avisos duplicados por repetición de la solicitud o del cambio de estado. El worker mantiene un ID estable ante reintentos, hasta tres intentos; un fallo del gateway no pierde el pedido. `Submitted` confirma aceptación por el gateway, no entrega al celular.

Pruebas: integración SQL en Foodies_Tests para privacidad, permisos, validación, ausencia de teléfono, duplicados y destinatarios; cancelación en los tres estados autorizados, motivo obligatorio, concurrencia, estados finales bloqueados y ausencia de SMS sin teléfono verificado. El backend pasó 62 pruebas. La simulación HTTP local pasó las 8 pruebas del gateway y cubrió PIN, pedido nuevo, listo, cancelación y fallo 500. El navegador con datos sintéticos comprobó diálogo, motivo, filtro Cancelados y vista del cliente. No se envían SMS reales en estas pruebas. Falta comprobar la entrega al celular del nuevo texto de cancelación con un pedido real.
