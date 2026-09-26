-- Horario de atención en dos turnos con receso.
-- Mañana: primer turno 09:30, último 12:30. Tarde: primer turno 15:00, último 20:00.
-- Turnos cada 30 min. hora_cierre y pausa_inicio marcan el FIN del último turno de cada bloque.
-- Ejecutar en SQL Editor de Supabase. No cambia qué días están activos.

update public.horario_laboral
set hora_apertura = time '09:30',
    pausa_inicio  = time '13:00',
    pausa_fin     = time '15:00',
    hora_cierre   = time '20:30',
    slot_minutos  = 30
where salon_id = '11111111-1111-1111-1111-111111111111';

alter table public.horario_laboral alter column slot_minutos set default 30;
