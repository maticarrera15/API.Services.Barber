-- Mails al cliente (confirmación y recordatorio de turno).
-- Ejecutar en SQL Editor de Supabase después de 006_cliente_cuenta.sql.

create table if not exists public.notificacion_email (
  id             uuid primary key default gen_random_uuid(),
  turno_id       uuid         not null references public.turno (id) on delete cascade,
  tipo           varchar(20)  not null,
  destinatario   varchar(120) not null,
  estado         varchar(20)  not null default 'pendiente',
  intentos       smallint     not null default 0,
  -- Id que devuelve el proveedor (Resend): sirve para cruzar los avisos de rebote.
  proveedor_id   varchar(100) null,
  error          text         null,
  enviado_at     timestamptz  null,
  created_at     timestamptz  not null default now(),
  updated_at     timestamptz  not null default now(),
  constraint notificacion_email_tipo_check check (tipo in ('confirmacion', 'recordatorio')),
  constraint notificacion_email_estado_check check (estado in ('pendiente', 'enviado', 'fallido', 'omitido')),
  -- Un mismo mail nunca sale dos veces para el mismo turno.
  constraint notificacion_email_turno_tipo_unique unique (turno_id, tipo)
);

drop trigger if exists trg_notificacion_email_updated_at on public.notificacion_email;
create trigger trg_notificacion_email_updated_at
  before update on public.notificacion_email
  for each row execute function public.set_updated_at();

create index if not exists idx_notificacion_email_pendientes
  on public.notificacion_email (created_at)
  where estado = 'pendiente';

create index if not exists idx_notificacion_email_proveedor
  on public.notificacion_email (proveedor_id)
  where proveedor_id is not null;

comment on table public.notificacion_email is
  'Mails transaccionales al cliente. Una fila por turno y tipo; estado pendiente/enviado/fallido/omitido.';

-- Se completa cuando el proveedor avisa que el email rebotó o fue marcado como spam: no se le vuelve a escribir.
alter table public.cliente
  add column if not exists email_rebotado_at timestamptz null;
