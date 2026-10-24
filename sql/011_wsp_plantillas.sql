-- Preferencia y plantillas de WhatsApp (confirmación / recordatorio).
-- Ejecutar en SQL Editor de Supabase después de 010_turno_fijo_precio.sql.

alter table public.salon
  add column if not exists wsp_confirmacion boolean not null default true,
  add column if not exists wsp_plantilla_confirmacion text null,
  add column if not exists wsp_plantilla_recordatorio text null;

comment on column public.salon.wsp_confirmacion is
  'Si true, al confirmar un turno se ofrece avisar por WhatsApp y el link en la tarjeta.';
comment on column public.salon.wsp_plantilla_confirmacion is
  'Plantilla del mensaje de confirmación. Placeholders: {cliente} {fecha} {hora} {servicio} {profesional} {salon} {direccion}. Null = default.';
comment on column public.salon.wsp_plantilla_recordatorio is
  'Plantilla del mensaje de recordatorio. Mismos placeholders. Null = default.';
