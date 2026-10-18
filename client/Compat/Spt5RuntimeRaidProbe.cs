using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace pitTeam.Compat
{
    internal static class Spt5RuntimeRaidProbe
    {
        internal static void RequireShadow()
        {
            Spt5RuntimeShadowProbePolicy.RequireEnabled(Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT")!);
        }
        private static Player QaPlayer(Vector3 target)
        {
            RequireShadow();
            ManagedFramePump.RequireOwnerThread();
            var player = GamePlayerOwner.MyPlayer ?? throw new InvalidOperationException("main-player-unavailable");
            if (player.ProfileId != "6ace6340c399ad84d0a90502" || player.HealthController?.IsAlive != true || Singleton<GameWorld>.Instance?.MainPlayer?.Pointer != player.Pointer)
                throw new InvalidOperationException("live-private-qa-player-required");
            if (!float.IsFinite(target.x) || !float.IsFinite(target.y) || !float.IsFinite(target.z) || Vector3.Distance(player.Position, target) > 300)
                throw new ArgumentOutOfRangeException(nameof(target));
            return player;
        }
        private static object Point(Vector3 p) => new { x = p.x, y = p.y, z = p.z };
        private static bool Reached(Vector3 actual, Vector3 waypoint)
        {
            Vector3 delta = actual - waypoint;
            return delta.x * delta.x + delta.z * delta.z <= 0.16f && Mathf.Abs(delta.y) <= 1f;
        }
        private static Vector3 NavOrigin(Vector3 actual)
        {
            if (!NavMesh.SamplePosition(actual, out NavMeshHit hit, 1f, NavMesh.AllAreas) ||
                Vector3.Distance(actual, hit.position) > 0.6f)
                throw new InvalidOperationException("player-not-near-walkable-navmesh");
            return hit.position;
        }

        private sealed class InputLease : IDisposable
        {
            private readonly Player player;
            private bool disposed;
            internal string? RestoreError;
            internal InputLease(Player player)
            {
                this.player = player;
                if (GamePlayerOwner.IgnoreInput) throw new InvalidOperationException("player-input-already-blocked");
                GamePlayerOwner.SetIgnoreInput(true, EIgnoreInputSource.Common);
                try { player.EnableSprint(false); player.InputDirection = Vector2.zero; player.Move(Vector2.zero); }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try { player.InputDirection = Vector2.zero; player.Move(Vector2.zero); }
                catch (Exception error) { RestoreError = error.ToString(); }
                finally
                {
                    try { GamePlayerOwner.SetIgnoreInput(false, EIgnoreInputSource.Common); }
                    catch (Exception error) { RestoreError = error.ToString(); }
                }
            }
            internal object Snapshot() => new { restored = disposed && !GamePlayerOwner.IgnoreInput, error = RestoreError };
        }

        private sealed class WalkingRoute
        {
            internal readonly Vector3 Endpoint;
            private Vector3[] corners = Array.Empty<Vector3>();
            private int corner, replans;
            private Vector3 progressPosition;
            private float progressTime;
            internal WalkingRoute(Vector3 current, Vector3 endpoint)
            {
                Endpoint = endpoint;
                Replan(current);
            }
            private void Replan(Vector3 current)
            {
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(NavOrigin(current), Endpoint, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("no-complete-navmesh-route");
                corners = path.corners; corner = 0;
                progressPosition = current; progressTime = Time.realtimeSinceStartup;
            }
            internal bool Tick(Player player)
            {
                Vector3 current = player.Position;
                while (corner < corners.Length && Reached(current, corners[corner])) corner++;
                if (corner >= corners.Length) return true;
                if (Vector3.Distance(current, progressPosition) > 0.2f)
                { progressPosition = current; progressTime = Time.realtimeSinceStartup; }
                if (Time.realtimeSinceStartup - progressTime > 8f)
                {
                    if (++replans > 2) throw new InvalidOperationException("native-walking-stalled:" + JsonSerializer.Serialize(Snapshot(current)));
                    Replan(current);
                }
                Vector3 delta = corners[corner] - current; delta.y = 0;
                if (delta.sqrMagnitude < 0.0001f) throw new InvalidOperationException("vertical-only-navmesh-waypoint");
                var movement = player.MovementContext;
                movement.Rotation = new Vector2(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, movement.Pitch);
                player.InputDirection = new Vector2(0, 1);
                player.Move(new Vector2(0, 1));
                return false;
            }
            internal object Snapshot(Vector3 current) => new { current = Point(current), endpoint = Point(Endpoint), corner,
                cornerCount = corners.Length, target = corner < corners.Length ? Point(corners[corner]) : null, replans };
        }
        internal static object Face(Vector3 target)
        {
            var player = QaPlayer(target);
            var movement = player.MovementContext ?? throw new InvalidOperationException("movement-context-unavailable");
            Vector3 origin = player.PlayerBones?.Head?.Original?.position ?? player.Position + Vector3.up * 1.6f;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude < 0.01f) throw new ArgumentException("look-target-too-close");
            Vector2 rotation = new Vector2(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg,
                Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -80, 80));
            movement.Rotation = rotation;
            return new { passed = true, method = "native MovementContext.Rotation; QA camera only", nativeInputIssued = false,
                origin = Point(origin), target = Point(target), yaw = movement.Yaw, pitch = movement.Pitch };
        }
        internal static Task<object> Navigate(Vector3 target)
        {
            var player = QaPlayer(target);
            var world = Singleton<GameWorld>.Instance;
            var completion = new TaskCompletionSource<object>();
            if (!NavMesh.SamplePosition(target, out NavMeshHit endpoint, 3f, NavMesh.AllAreas)) throw new InvalidOperationException("target-not-on-navmesh");
            var route = new WalkingRoute(player.Position, endpoint.position);
            var input = new InputLease(player);
            var moves = new List<object>();
            float started = Time.realtimeSinceStartup, nextSample = started;
            Vector3 previous = player.Position;
            ManagedFramePump.Enqueue(() =>
            {
                try
                {
                    if (Time.realtimeSinceStartup - started > 100) throw new TimeoutException("native-route-timeout");
                    if (Singleton<GameWorld>.Instance?.Pointer != world.Pointer || player.HealthController?.IsAlive != true)
                        throw new InvalidOperationException("raid-ended-during-navigation");
                    Vector3 current = player.Position;
                    if (Time.realtimeSinceStartup >= nextSample)
                    {
                        moves.Add(new { realtime = Time.realtimeSinceStartup, before = Point(previous), after = Point(current), distance = Vector3.Distance(previous, current) });
                        previous = current; nextSample = Time.realtimeSinceStartup + 0.5f;
                    }
                    if (route.Tick(player))
                    {
                        input.Dispose();
                        completion.TrySetResult(new { passed = input.RestoreError == null, method = "QA native Player.Move along complete NavMesh path; normal collision, no teleport", nativeInputIssued = false,
                            target = Point(target), endpoint = Point(endpoint.position), after = Point(current), moves, elapsed = Time.realtimeSinceStartup - started, input = input.Snapshot() });
                        return true;
                    }
                    return false;
                }
                catch (Exception error) { input.Dispose(); completion.TrySetResult(new { passed = false, error = error.ToString(), moves, route = route.Snapshot(player.Position), input = input.Snapshot() }); return true; }
            });
            return completion.Task;
        }
        internal static Task<object> Follow(string id)
        {
            RequireShadow();
            ManagedFramePump.RequireOwnerThread();
            var player = QaPlayer(GamePlayerOwner.MyPlayer?.Position ?? Vector3.zero);
            var world = Singleton<GameWorld>.Instance;
            var completion = new TaskCompletionSource<object>();
            var samples = new List<string>(); var moves = new List<object>();
            float started = Time.realtimeSinceStartup, next = started, nextMove = started;
            WalkingRoute? walking = null; int route = 0;
            var input = new InputLease(player);
            Vector3 previous = player.Position;
            Vector3 heading = player.LookDirection; heading.y = 0; heading.Normalize();
            string root = Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT")!;
            void Sample()
            {
                string file = Path.Combine(root, "probe-" + id + "-sample-" + samples.Count.ToString("D3") + ".json");
                if (File.Exists(file)) throw new InvalidOperationException("sample-collision:" + file);
                string json = JsonSerializer.Serialize(new { id, operation = "world", timestamp = DateTimeOffset.UtcNow, pid = Environment.ProcessId, result = Spt5RuntimeWorldProbe.CaptureInstallSample() });
                File.WriteAllText(file + ".writing", json); File.Move(file + ".writing", file); samples.Add(file);
            }
            ManagedFramePump.Enqueue(() =>
            {
                try
                {
                    RequireShadow();
                    float now = Time.realtimeSinceStartup;
                    if (now >= next) { Sample(); next = now + 2f; }
                    if (now - started >= 70f)
                    { input.Dispose(); completion.TrySetResult(new { passed = input.RestoreError == null, elapsed = now - started, samplePaths = samples, moves, nativeInputIssued = false, method = "native Player.Move; normal collision, no teleport", input = input.Snapshot() }); return true; }
                    if (player.HealthController?.IsAlive != true || Singleton<GameWorld>.Instance?.Pointer != world.Pointer) throw new InvalidOperationException("raid-player-no-longer-alive");
                    Vector3 current = player.Position;
                    if (now >= nextMove)
                    {
                        float distance = Vector3.Distance(previous, current);
                        moves.Add(new { realtime = now, before = Point(previous), after = Point(current), requestedDistance = distance, actualDistance = distance });
                        previous = current; nextMove = now + 0.5f;
                    }
                    if (walking == null || walking.Tick(player))
                    {
                        bool found = false;
                        for (int i = 0; i < 32; i++)
                        {
                            float angle = (i / 2 + 1) * 22.5f * (i % 2 == 0 ? 1f : -1f);
                            Vector3 direction = Quaternion.Euler(0, angle, 0) * heading;
                            Vector3 desired = current + direction * (12f + route % 3 * 4f);
                            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas) || Vector3.Distance(current, hit.position) < 8f) continue;
                            try { walking = new WalkingRoute(current, hit.position); }
                            catch (InvalidOperationException) { continue; }
                            heading = direction; route++; found = true; break;
                        }
                        if (!found) throw new InvalidOperationException("no-complete-navmesh-route");
                    }
                    return false;
                }
                catch (Exception ex) { input.Dispose(); completion.TrySetResult(new { passed = false, error = ex.ToString(), samplePaths = samples, moves, route = walking?.Snapshot(player.Position), input = input.Snapshot() }); return true; }
            });
            return completion.Task;
        }
    }
}
