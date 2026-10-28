-- Descripción del salón (solo lectura en la app del barbero) + estados Completado / No_asistio.
-- Ejecutar en SQL Editor de Supabase después de 010_turno_fijo_precio.sql.

alter table public.salon
  add column if not exists descripcion text null;

comment on column public.salon.descripcion is
  'Texto libre del salón (quiénes son, estilo, etc.). Visible en Datos personales.';

insert into public.estado_turno (id, codigo, nombre, es_final, bloquea_slot)
values
  (4, 'Completado', 'Completado', true, true),
  (5, 'No_asistio', 'No asistió', true, false)
on conflict (id) do nothing;
