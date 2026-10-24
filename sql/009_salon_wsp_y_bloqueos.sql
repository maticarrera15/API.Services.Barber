-- Preferencia de recordatorio por WhatsApp + índice útil para bloqueos de agenda.
-- Ejecutar en SQL Editor de Supabase después de 008_salon_mail_prefs.sql.

alter table public.salon
  add column if not exists wsp_recordatorio boolean not null default true;

comment on column public.salon.wsp_recordatorio is
  'Si true, en la agenda del profesional aparece el botón para recordar el turno por WhatsApp.';

create index if not exists idx_bloqueo_agenda_rango
  on public.bloqueo_agenda (salon_id, inicio, fin);
