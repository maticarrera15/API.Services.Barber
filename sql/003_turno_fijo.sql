-- Turnos fijos semanales (cliente recurrente).
-- Ejecutar en SQL Editor de Supabase después del ddl principal.

create table if not exists public.turno_fijo (
  id                 uuid primary key default gen_random_uuid(),
  salon_id           uuid         not null references public.salon (id) on delete cascade,
  profesional_id     uuid         not null references public.profesional (id) on delete cascade,
  servicio_id        uuid         null references public.servicio (id) on delete set null,
  cliente_nombre     varchar(120) not null,
  cliente_email      varchar(120) not null,
  cliente_telefono   varchar(40),
  dia_semana         smallint     not null check (dia_semana between 0 and 6),
  hora               time         not null,
  duracion_min       integer      not null default 45 check (duracion_min > 0),
  activo             boolean      not null default true,
  notas              varchar(200),
  created_at         timestamptz  not null default now(),
  updated_at         timestamptz  not null default now()
);

create unique index if not exists turno_fijo_pro_dia_hora_uidx
  on public.turno_fijo (profesional_id, dia_semana, hora)
  where activo = true;

create index if not exists idx_turno_fijo_profesional
  on public.turno_fijo (profesional_id, dia_semana)
  where activo = true;

drop trigger if exists trg_turno_fijo_updated_at on public.turno_fijo;
create trigger trg_turno_fijo_updated_at
  before update on public.turno_fijo
  for each row execute function public.set_updated_at();

comment on table public.turno_fijo is
  'Cliente fijo semanal. La agenda lo trata como ocupación recurrente sin crear filas en turno.';
