-- Notificaciones del profesional (campanita) y suscripciones Web Push por dispositivo.
-- Ejecutar en SQL Editor de Supabase después del ddl principal.

create table if not exists public.notificacion (
  id              uuid primary key default gen_random_uuid(),
  salon_id        uuid         not null references public.salon (id) on delete cascade,
  profesional_id  uuid         not null references public.profesional (id) on delete cascade,
  turno_id        uuid         null references public.turno (id) on delete cascade,
  tipo            varchar(40)  not null,
  titulo          varchar(120) not null,
  detalle         varchar(240),
  leida_at        timestamptz  null,
  created_at      timestamptz  not null default now()
);

create index if not exists idx_notificacion_profesional_fecha
  on public.notificacion (profesional_id, created_at desc);

create index if not exists idx_notificacion_no_leidas
  on public.notificacion (profesional_id)
  where leida_at is null;

create index if not exists idx_notificacion_turno
  on public.notificacion (turno_id)
  where turno_id is not null;

comment on table public.notificacion is
  'Avisos al profesional (ej. turno solicitado por un cliente). Base para push, email o WhatsApp.';

create table if not exists public.push_suscripcion (
  id              uuid primary key default gen_random_uuid(),
  profesional_id  uuid         not null references public.profesional (id) on delete cascade,
  endpoint        text         not null,
  p256dh          text         not null,
  auth            text         not null,
  user_agent      varchar(200),
  created_at      timestamptz  not null default now(),
  constraint push_suscripcion_endpoint_unique unique (endpoint)
);

create index if not exists idx_push_suscripcion_profesional
  on public.push_suscripcion (profesional_id);

comment on table public.push_suscripcion is
  'Un registro por dispositivo/navegador del profesional suscripto a Web Push.';
