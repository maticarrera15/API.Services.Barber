-- Usuario de login para Ivan Roasso (profesional seed).
-- Ejecutar en SQL Editor de Supabase DESPUÉS de ddl.sql y 001_usuario.sql.
--
-- Email:    ivan@navaja.barber
-- Password: Navaja2026!
--
-- Si ya existe un usuario para ese email, este script no lo duplica (ON CONFLICT DO NOTHING).

insert into public.usuario (id, email, password_hash, profesional_id, rol, activo)
values (
  'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  'ivan@navaja.barber',
  '$2a$11$PkJS2kkEKfuPfYirSVBW3.7iW49okQLoLAy4i2qCr3q12feRGIfhS',
  '22222222-2222-2222-2222-222222222222',
  'profesional',
  true
)
on conflict (email) do nothing;
