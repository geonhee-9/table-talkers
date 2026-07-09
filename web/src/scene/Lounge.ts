// Cozy lounge — warm dim light, dark walls, wood floor, rug, hanging lamp, soft fog.
// The round table and its chairs are DYNAMIC: they rebuild whenever the seat layout
// changes (2 people face each other, 3 at 120°, … up to 10), and the table grows too.
import * as THREE from 'three';
import { seatLayout } from '../core/seats';

export function buildLounge(scene: THREE.Scene): void {
  scene.background = new THREE.Color(0x171210);
  scene.fog = new THREE.FogExp2(0x1a120d, 0.055);

  scene.add(new THREE.AmbientLight(0xffe0c4, 0.45));

  const sun = new THREE.DirectionalLight(0xffe0b8, 0.5);
  sun.position.set(-3, 5, -2);
  sun.castShadow = true;
  scene.add(sun);

  const lamp = new THREE.PointLight(0xffb873, 20, 9, 1.8);
  lamp.position.set(0, 2.05, 0);
  lamp.castShadow = true;
  scene.add(lamp);

  const mat = {
    floor: new THREE.MeshStandardMaterial({ color: 0x54402e, roughness: 0.7 }),
    wall: new THREE.MeshStandardMaterial({ color: 0x3d3028, roughness: 0.9 }),
    rug: new THREE.MeshStandardMaterial({ color: 0x6b3328, roughness: 0.95 }),
    table: new THREE.MeshStandardMaterial({ color: 0x734c30, roughness: 0.5 }),
    chair: new THREE.MeshStandardMaterial({ color: 0x4d3828, roughness: 0.8 }),
    bulb: new THREE.MeshStandardMaterial({
      color: 0xffd9a0, emissive: 0xffa04d, emissiveIntensity: 2.5,
    }),
    cord: new THREE.MeshStandardMaterial({ color: 0x1a1a1a }),
  };

  const add = (geo: THREE.BufferGeometry, m: THREE.Material, x: number, y: number, z: number, yawDeg = 0) => {
    const mesh = new THREE.Mesh(geo, m);
    mesh.position.set(x, y, z);
    mesh.rotation.y = (yawDeg * Math.PI) / 180;
    mesh.receiveShadow = true;
    mesh.castShadow = true;
    scene.add(mesh);
    return mesh;
  };

  // Shell
  const floor = add(new THREE.PlaneGeometry(12, 12), mat.floor, 0, 0, 0);
  floor.rotation.x = -Math.PI / 2;
  floor.castShadow = false;
  add(new THREE.CylinderGeometry(3, 3, 0.02, 32), mat.rug, 0, 0.011, 0).castShadow = false;
  add(new THREE.BoxGeometry(12, 3, 0.2), mat.wall, 0, 1.5, 6);
  add(new THREE.BoxGeometry(12, 3, 0.2), mat.wall, 0, 1.5, -6);
  add(new THREE.BoxGeometry(0.2, 3, 12), mat.wall, 6, 1.5, 0);
  add(new THREE.BoxGeometry(0.2, 3, 12), mat.wall, -6, 1.5, 0);
  add(new THREE.BoxGeometry(12, 0.1, 12), mat.wall, 0, 3.05, 0).castShadow = false;

  // Lamp fixture
  add(new THREE.SphereGeometry(0.11, 16, 12), mat.bulb, 0, 2.15, 0).castShadow = false;
  add(new THREE.CylinderGeometry(0.01, 0.01, 0.9, 6), mat.cord, 0, 2.65, 0).castShadow = false;

  // Dynamic table + chairs: rebuilt whenever the seat count changes.
  const tableGroup = new THREE.Group();
  scene.add(tableGroup);

  const rebuildTable = (): void => {
    // Dispose previous meshes before rebuilding.
    for (const child of [...tableGroup.children]) {
      child.traverse((o) => {
        if (o instanceof THREE.Mesh) o.geometry.dispose();
      });
      tableGroup.remove(child);
    }

    const r = seatLayout.tableRadius();
    const table = new THREE.Mesh(new THREE.CylinderGeometry(r, r * 0.92, 0.8, 40), mat.table);
    table.position.y = 0.4;
    table.castShadow = true;
    table.receiveShadow = true;
    tableGroup.add(table);

    for (let i = 0; i < seatLayout.seatCount; i++) {
      const a = seatLayout.anchor(i);
      const chair = new THREE.Group();
      chair.position.set(a.x, 0, a.z);
      chair.rotation.y = (a.yawDeg * Math.PI) / 180;
      const seatMesh = new THREE.Mesh(new THREE.BoxGeometry(0.52, 0.08, 0.52), mat.chair);
      seatMesh.position.y = 0.45;
      const back = new THREE.Mesh(new THREE.BoxGeometry(0.52, 0.7, 0.07), mat.chair);
      back.position.set(0, 0.85, -0.26);
      const legs = new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.4, 0.46), mat.chair);
      legs.position.y = 0.2;
      for (const m of [seatMesh, back, legs]) {
        m.castShadow = true;
        m.receiveShadow = true;
        chair.add(m);
      }
      tableGroup.add(chair);
    }
  };

  rebuildTable();
  seatLayout.onChanged.add(rebuildTable);
}
