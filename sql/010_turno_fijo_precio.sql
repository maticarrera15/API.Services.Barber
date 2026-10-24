-- Precio preferencial del cliente fijo semanal (beneficio por turno recurrente).
-- Ejecutar en SQL Editor de Supabase después de 009_salon_wsp_y_bloqueos.sql.

alter table public.turno_fijo
  add column if not exists precio_centavos integer null
    check (precio_centavos is null or precio_centavos >= 0);

comment on column public.turno_fijo.precio_centavos is
  'Precio acordado del turno semanal (centavos). Null = usar precio del servicio si hay uno.';
