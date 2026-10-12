-- Cuentas de cliente (opcionales: se puede seguir reservando como invitado).
-- Ejecutar en SQL Editor de Supabase después de 005_notificaciones.sql.

create table if not exists public.cliente_cuenta (
  id                   uuid primary key default gen_random_uuid(),
  cliente_id           uuid         not null references public.cliente (id) on delete cascade,
  email                varchar(120) not null,
  -- null cuando la cuenta solo entra con un proveedor externo (ej. Google).
  password_hash        varchar(200) null,
  email_verificado_at  timestamptz  null,
  activo               boolean      not null default true,
  ultimo_login_at      timestamptz  null,
  created_at           timestamptz  not null default now(),
  updated_at           timestamptz  not null default now(),
  constraint cliente_cuenta_email_unique unique (email),
  constraint cliente_cuenta_cliente_unique unique (cliente_id)
);

drop trigger if exists trg_cliente_cuenta_updated_at on public.cliente_cuenta;
create trigger trg_cliente_cuenta_updated_at
  before update on public.cliente_cuenta
  for each row execute function public.set_updated_at();

comment on table public.cliente_cuenta is
  'Login del cliente. 1 a 1 con cliente; los turnos de invitado con el mismo email no se vinculan hasta verificar el email.';

-- Turnos sacados con la sesión de cliente iniciada ("Mis turnos").
alter table public.turno
  add column if not exists cuenta_id uuid null references public.cliente_cuenta (id) on delete set null;

create index if not exists idx_turno_cuenta_inicio
  on public.turno (cuenta_id, inicio desc)
  where cuenta_id is not null;
