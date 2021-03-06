import unittest
import krpctest

class TestPartsSolarPanel(krpctest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.new_save()
        cls.launch_vessel_from_vab('PartsSolarPanel')
        cls.remove_other_vessels()
        cls.vessel = cls.connect().space_center.active_vessel
        parts = cls.vessel.parts
        cls.state = cls.connect().space_center.SolarPanelState
        cls.panel = parts.with_title('Gigantor XL Solar Array')[0].solar_panel
        cls.fixed_panel = parts.with_title('OX-STAT Photovoltaic Panels')[0].solar_panel
        cls.panel_break = parts.with_title('OX-4L 1x6 Photovoltaic Panels')[0].solar_panel

    def test_fixed_panel(self):
        self.assertTrue(self.fixed_panel.deployed)
        self.assertEqual(self.state.extended, self.fixed_panel.state)
        self.assertGreater(self.fixed_panel.energy_flow, 0)
        self.assertGreater(self.fixed_panel.sun_exposure, 0)

    def test_extendable_panel(self):
        self.assertFalse(self.panel.deployed)
        self.assertEqual(self.state.retracted, self.panel.state)
        self.assertEqual(0, self.panel.energy_flow)
        self.assertEqual(0, self.panel.sun_exposure)

        self.panel.deployed = True
        self.wait()

        self.assertTrue(self.panel.deployed)
        self.assertEqual(self.state.extending, self.panel.state)
        self.assertEqual(0, self.panel.energy_flow)
        self.assertEqual(0, self.panel.sun_exposure)

        while self.panel.state == self.state.extending:
            self.wait()

        self.assertTrue(self.panel.deployed)
        self.assertEqual(self.state.extended, self.panel.state)
        self.assertGreater(self.panel.energy_flow, 0)
        self.assertGreater(self.panel.sun_exposure, 0)

        self.panel.deployed = False
        self.wait()

        self.assertFalse(self.panel.deployed)
        self.assertEqual(self.state.retracting, self.panel.state)
        self.assertEqual(0, self.panel.energy_flow)
        self.assertEqual(0, self.panel.sun_exposure)

        while self.panel.state == self.state.retracting:
            self.wait()

        self.assertFalse(self.panel.deployed)
        self.assertEqual(self.state.retracted, self.panel.state)
        self.assertEqual(0, self.panel.energy_flow)
        self.assertEqual(0, self.panel.sun_exposure)

    def test_break_panel(self):
        self.assertEqual(self.state.retracted, self.panel_break.state)
        self.panel_break.deployed = True
        while self.panel_break.state == self.state.extending:
            self.wait()
        self.vessel.control.activate_next_stage()
        self.wait(1)
        self.assertEqual(self.state.broken, self.panel_break.state)

if __name__ == '__main__':
    unittest.main()
