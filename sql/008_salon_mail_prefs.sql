-- Preferencias de mails al cliente (por salón).
-- Ejecutar en SQL Editor de Supabase después de 007_notificacion_email.sql.

alter table public.salon
  add column if not exists mail_confirmacion boolean not null default true,
  add column if not exists mail_recordatorio boolean not null default true,
  add column if not exists mail_whatsapp_btn boolean not null default false;

comment on column public.salon.mail_confirmacion is
  'Si true, al confirmar un turno se programa el mail de confirmación al cliente.';
comment on column public.salon.mail_recordatorio is
  'Si true, el job de recordatorios programa el mail 3 h antes del turno.';
comment on column public.salon.mail_whatsapp_btn is
  'Si true, el mail incluye el botón "Escribinos por WhatsApp".';
