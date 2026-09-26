-- Auth JWT propio de la API (no usa auth.users de Supabase).
-- Ejecutar en SQL Editor después del ddl.sql principal.

create table if not exists public.usuario (
  id              uuid primary key default gen_random_uuid(),
  email           varchar(120) not null,
  password_hash   varchar(200) not null,
  profesional_id  uuid         not null references public.profesional (id) on delete cascade,
  rol             varchar(40)  not null default 'profesional',
  activo          boolean      not null default true,
  created_at      timestamptz  not null default now(),
  updated_at      timestamptz  not null default now(),
  constraint usuario_email_unique unique (email),
  constraint usuario_profesional_unique unique (profesional_id)
);

create index if not exists idx_usuario_profesional on public.usuario (profesional_id);

drop trigger if exists trg_usuario_updated_at on public.usuario;
create trigger trg_usuario_updated_at
  before update on public.usuario
  for each row execute function public.set_updated_at();

comment on table public.usuario is 'Login del profesional. JWT lo emite Api.Services.Barber';
